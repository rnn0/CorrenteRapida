using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CorrenteRapida;

/// <summary>
/// Descobre (só no cliente) quando o jogador abate um inimigo, para a joia ativa "absorver" o alvo.
/// O cliente não sabe com certeza quem deu o golpe final, então conta como abate um inimigo que
/// morreu poucos segundos depois de ter sido acertado pelo jogador. "Acertado" vem de duas fontes:
/// os números de dano (que trazem quem bateu e em quem) e, se esses não aparecerem, a vida do inimigo
/// caindo logo depois de um ataque do jogador mirando nele.
/// </summary>
internal sealed class KillWatcher
{
    /// <summary>Abates detectados (posição no mundo), esvaziados pela barra a cada quadro.</summary>
    public readonly Queue<(Vector3 Pos, bool VBlood)> Kills = new();

    World? _world;
    float _nextWorldLookup, _nextPoll;
    Entity _me = Entity.Null;
    float _nextMeLookup;
    EntityQuery _qMe, _qSct, _qUnits;
    bool _queries;
    int _errors;
    bool _loggedSct, _loggedHeur, _loggedKill;

    sealed class Track { public float HitAt; public Vector3 Pos; public float LastHp = -1f; public bool VBlood; }
    readonly Dictionary<long, Track> _hit = new();
    readonly Dictionary<long, float> _hp = new();          // vida da última leitura (para a regra do ataque)
    readonly Dictionary<long, Entity> _ent = new();
    float _lastAttack = -99f;
    Entity _lastHover = Entity.Null;
    float _lastHoverAt = -99f;

    static long Key(Entity e) => ((long)e.Index << 32) | (uint)e.Version;

    public void Update()
    {
        float now = Time.unscaledTime;
        if (now < _retryAt) return;
        try { Tick(now); }
        catch (Exception e)
        {
            // Não desiste de vez: registra os primeiros erros (com onde aconteceram) e tenta de novo depois.
            if (_errors++ < 4) Plugin.L.LogWarning($"[CorrenteRapida] Abates: erro na etapa '{_stage}': {e}");
            _retryAt = now + 5f;
            _world = null; _queries = false; _me = Entity.Null;
        }
    }

    float _retryAt;
    string _stage = "";
    float _nextDiag;
    int _diagCount;

    static bool TryPos(EntityManager em, Entity e, out Vector3 p)
    {
        if (em.HasComponent<Translation>(e)) { var v = em.GetComponentData<Translation>(e).Value; p = new Vector3(v.x, v.y, v.z); return true; }
        if (em.HasComponent<LocalTransform>(e)) { var v = em.GetComponentData<LocalTransform>(e).Position; p = new Vector3(v.x, v.y, v.z); return true; }
        if (em.HasComponent<LocalToWorld>(e)) { var v = em.GetComponentData<LocalToWorld>(e).Value.c3; p = new Vector3(v.x, v.y, v.z); return true; }
        p = Vector3.zero; return false;
    }

    void Tick(float now)
    {
        _stage = "teclas";
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E)
            || Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.T)) _lastAttack = now;

        _stage = "mundo";
        if (_world == null || !_world.IsCreated)
        {
            if (now < _nextWorldLookup) return;
            _nextWorldLookup = now + 3f;
            _world = null;
            foreach (var w in World.s_AllWorlds) if (w.Name == "Client_0") { _world = w; break; }
            if (_world == null) return;
            _queries = false;
        }
        var em = _world.EntityManager;
        if (!_queries)
        {
            _stage = "consultas";
            _qMe = em.CreateEntityQuery(new[] { ComponentType.ReadOnly(Il2CppType.Of<LocalCharacter>()) });
            _qSct = em.CreateEntityQuery(new[] { ComponentType.ReadOnly(Il2CppType.Of<ScrollingCombatTextMessage>()) });
            _qUnits = em.CreateEntityQuery(new[] { ComponentType.ReadOnly(Il2CppType.Of<Health>()), ComponentType.ReadOnly(Il2CppType.Of<AggroConsumer>()) });
            _queries = true;
        }
        _stage = "personagem";
        if (_me == Entity.Null || !em.Exists(_me))
        {
            if (now < _nextMeLookup) return;
            _nextMeLookup = now + 1f;
            var arr = _qMe.ToEntityArray(Allocator.Temp);
            _me = arr.Length > 0 ? arr[0] : Entity.Null;
            arr.Dispose();
            if (_me == Entity.Null) return;
        }

        // Números de dano: lidos todo quadro (as mensagens duram pouco).
        _stage = "numeros de dano";
        if (_qSct.CalculateEntityCount() > 0)
        {
            var arr = _qSct.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < arr.Length; i++)
            {
                var m = em.GetComponentData<ScrollingCombatTextMessage>(arr[i]);
                var src = m.Source._Entity; var tgt = m.Target._Entity;
                if (tgt == Entity.Null || tgt == _me || !IsMine(em, src)) continue;
                if (!em.Exists(tgt) || !em.HasComponent<Health>(tgt) || em.HasComponent<PlayerCharacter>(tgt)) continue;
                MarkHit(tgt, new Vector3(m.Position.x, m.Position.y, m.Position.z), now);
                if (!_loggedSct) { _loggedSct = true; Plugin.L.LogInfo("[CorrenteRapida] Abates: acertos detectados pelos números de dano."); }
            }
            arr.Dispose();
        }

        // Para onde estou mirando.
        _stage = "mira";
        Vector3 aim = Vector3.zero; bool hasAim = false;
        if (em.HasComponent<EntityInput>(_me))
        {
            var inp = em.GetComponentData<EntityInput>(_me);
            if (inp.HoveredEntity != Entity.Null) { _lastHover = inp.HoveredEntity; _lastHoverAt = now; }
            aim = new Vector3(inp.AimPosition.x, inp.AimPosition.y, inp.AimPosition.z); hasAim = aim != Vector3.zero;
        }

        if (now < _nextPoll) return;
        _nextPoll = now + 0.15f;
        _stage = "posicao";
        if (!TryPos(em, _me, out var me3)) return;

        // Inimigos perto. Conta como "acertado por mim" quando a vida cai logo depois de um ataque meu e
        // o inimigo está mirado, perto, ou brigando comigo (alvo do aggro dele sou eu).
        _stage = "inimigos";
        bool attacking = now - _lastAttack < 1.5f;
        var units = _qUnits.ToEntityArray(Allocator.Temp);
        int near = 0;
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i];
            if (u == _me || !TryPos(em, u, out var p)) continue;
            if ((p - me3).sqrMagnitude > 25f * 25f) continue;
            if (em.HasComponent<PlayerCharacter>(u)) continue;
            near++;
            var hp = em.GetComponentData<Health>(u);
            long k = Key(u);
            _ent[k] = u;
            bool hadHp = _hp.TryGetValue(k, out float last);
            bool dropped = hadHp && hp.Value < last - 0.01f;
            bool died = hadHp && last > 0f && (hp.IsDead || hp.Value <= 0f);
            if ((dropped || died) && attacking)
            {
                bool aimed = (_lastHover == u && now - _lastHoverAt < 2.5f)
                    || (hasAim && (new Vector2(p.x - aim.x, p.z - aim.z)).sqrMagnitude < 3.5f * 3.5f)
                    || (p - me3).sqrMagnitude < 6f * 6f
                    || AggroOnMe(em, u);
                if (aimed)
                {
                    MarkHit(u, p + Vector3.up * 1.2f, now);
                    if (!_loggedHeur) { _loggedHeur = true; Plugin.L.LogInfo("[CorrenteRapida] Abates: acertos detectados pela vida do alvo."); }
                }
            }
            _hp[k] = hp.IsDead ? 0f : hp.Value;
            if (_hit.TryGetValue(k, out var tr)) tr.Pos = p + Vector3.up * 1.2f;
        }
        units.Dispose();

        if (now >= _nextDiag && _diagCount < 3)
        {
            _nextDiag = now + 20f; _diagCount++;
            Plugin.L.LogInfo($"[CorrenteRapida] Abates: monitorando ({near} inimigos por perto, {_hit.Count} acertados).");
        }

        // Alvos acertados que morreram: abate.
        _stage = "abates";
        List<long>? drop = null;
        foreach (var kv in _hit)
        {
            var tr = kv.Value;
            bool dead = false;
            if (_ent.TryGetValue(kv.Key, out var ent))
            {
                if (!em.Exists(ent)) dead = now - tr.HitAt < 3f;
                else if (em.HasComponent<Dead>(ent)) dead = true;
                else if (em.HasComponent<Health>(ent)) { var h = em.GetComponentData<Health>(ent); dead = h.IsDead || h.Value <= 0f; }
            }
            if (dead)
            {
                Kills.Enqueue((tr.Pos, tr.VBlood));
                if (!_loggedKill) { _loggedKill = true; Plugin.L.LogInfo("[CorrenteRapida] Abates: primeiro abate detectado."); }
                if (tr.VBlood) Plugin.L.LogInfo("[CorrenteRapida] Abates: V Blood abatido!");
                (drop ??= new()).Add(kv.Key);
            }
            else if (now - tr.HitAt > (tr.VBlood ? 40f : 12f)) (drop ??= new()).Add(kv.Key);
        }
        if (drop != null) foreach (var k in drop) _hit.Remove(k);
        if (_hp.Count > 400)
        {
            _hp.Clear();
            var keep = new Dictionary<long, Entity>();
            foreach (var k in _hit.Keys) if (_ent.TryGetValue(k, out var en)) keep[k] = en;
            _ent.Clear(); foreach (var kv in keep) _ent[kv.Key] = kv.Value;
        }
    }

    bool AggroOnMe(EntityManager em, Entity u)
    {
        var a = em.GetComponentData<AggroConsumer>(u);
        return a.AggroTarget._Entity == _me || a.AlertTarget._Entity == _me;
    }

    bool IsMine(EntityManager em, Entity src)
    {
        if (src == Entity.Null) return false;
        if (src == _me) return true;
        if (em.Exists(src) && em.HasComponent<EntityOwner>(src)) return em.GetComponentData<EntityOwner>(src).Owner == _me;
        return false;
    }

    void MarkHit(Entity e, Vector3 pos, float now)
    {
        long k = Key(e);
        _ent[k] = e;
        if (!_hit.TryGetValue(k, out var tr))
        {
            _hit[k] = tr = new Track();
            var em = _world!.EntityManager;
            tr.VBlood = em.Exists(e) && (em.HasComponent<VBloodUnit>(e) || em.HasComponent<VBloodConsumeSource>(e));
        }
        tr.HitAt = now;
        if (pos != Vector3.zero) tr.Pos = pos;
    }
}
