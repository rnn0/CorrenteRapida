<p align="center"><img src="icon.png" width="128" alt="CorrenteRapida"></p>

# CorrenteRapida

Mod de cliente (BepInEx) para o servidor **Sangria Falls** de V Rising. Adiciona uma barra com as seis **Correntes** do Draekhar ao lado do botão "Trocar de Corrente": **um clique troca de elemento, sem abrir a janela.**

<p align="center"><img src="docs/lendaria.gif" alt="As seis Correntes no nível Lendário"></p>

## Recursos

- **Troca com um clique** (botão esquerdo ou direito). O mod aperta os próprios botões "USAR" do ScarletInterface: nenhuma mensagem é forjada e recarga/bloqueios continuam sendo controlados pelo servidor.
- **Sem abrir a janela**: o mod guarda os botões e as ações da janela original e reaproveita depois, mesmo com ela fechada.
- **Detecta trocas manuais** feitas pela janela original.
- **Pré-carregamento invisível**: ao entrar no servidor, o mod abre e fecha a janela sem que ela apareça, para já conhecer os botões.
- **Prévia do Lendário**: segure **Alt** com o mouse sobre uma bola para vê-la no nível máximo, com uma legenda do tempo de uso.
- **Joias vivas**: cada Corrente tem uma joia animada com ritmo próprio. Quando ativa, a joia se solta do encaixe e surge a aura do elemento.
- **Leve**: cada joia é desenhada a partir de um único atlas, num Canvas próprio, com limite de quadros (a ativa a 40 fps, as encaixadas a 15 fps).
- **Espaço automático**: a barra abre o espaço necessário para a aura de uma joia nunca ficar em cima da vizinha.

| Corrente | Joia | Ritmo e aura | Lendária |
|---|---|---|---|
| Glacial | vulto de um vampiro dormindo num bloco de gelo | coração congelado batendo devagar, ondas de geada e névoa de túmulo | asas de morcego de gelo e mandíbula de presas |
| Caos | bola de energia oscilante com runas em órbita, presa por correntes de magia | escapa, as correntes grudam de volta; fogo roxo e fendas no espaço | o selo se rompe: círculo mágico e runas se abrem quando ela escapa |
| Tempestade | raio de cristal dourado sobre um globo de plasma | carga e descarga: relâmpago cai na joia | bobinas com anéis alimentando o globo |
| Corrompida | olho de serpente vivo | respiração lenta, miasma venenoso, tentáculos segurando o olho | tentáculos pingando veneno e olhos na escuridão |
| Luminosa | sol perolado com luz prismática | halo, feixes de arco-íris | coroa, mandala e esferas de luz |
| Sanguínea | gota de rubi com sangue líquido, acorrentada | batimento de coração; a gota tenta fugir das correntes | gola de vampiro, chifres, morcegos e olhos na escuridão |

## Evolução por tempo de uso

Cada Corrente evolui conforme o tempo que você passa com ela ativa (pausa após 5 min sem mexer o mouse/teclado):

| Nível | Tempo | Visual |
|---|---|---|
| Bruta | 0 h | joia lapidada |
| Lapidada | 1 h | lapidação fina, mais brilho |
| Brônzea | 2 h | aro de bronze |
| Prateada | 3 h | aro de prata |
| Dourada | 4 h | aro de ouro com gemas |
| Lustrosa | 5 h | aura mais intensa, mais partículas |
| Radiante | 20 h | luz interna pulsando e fragmentos orbitando |
| Lendária | 50 h | segunda camada de aura e um traço temático na aura |

<p align="center"><img src="docs/niveis.png" alt="Os oito níveis de cada Corrente"></p>

Um anel fino em volta de cada bola mostra o progresso até o próximo nível. O tempo fica salvo só no seu PC, em `BepInEx/config/CorrenteRapida_tempo.txt`.

## Instalação

1. Tenha o **BepInEx 6 (IL2CPP)** e o **ScarletInterface** (já vêm com o launcher do Sangria Falls).
2. Baixe o `CorrenteRapida.dll` na página de [Releases](../../releases) e coloque em `BepInEx/plugins`.
3. Entre no servidor e abra a janela "Trocar de Corrente" uma vez pelo botão normal: o mod aprende onde ele fica e a barra aparece.

## Atualização automática

O mod consulta a última Release deste repositório ao abrir o jogo. Em `[Atualizacao] Modo`:

- **Automatico** (padrão a partir da 2.5.2): baixa a DLL nova, confere o SHA-256 publicado na Release e a instala; ela passa a valer na próxima vez que o jogo abrir.
- **Avisar**: mostra um aviso embaixo da barra quando há versão nova; clicando no aviso, ela é baixada e instalada.
- **Desligado**: não consulta nada.

O mod só baixa deste repositório, e tudo o que faz fica registrado no `LogOutput.log`.

## Configuração

Arquivo `BepInEx/config/sangria.correnterapida.cfg` (criado na primeira execução). Destaques:

| Opção | Padrão | O que faz |
|---|---|---|
| `Modo` (Atualizacao) | `Automatico` | `Automatico`, `Avisar` ou `Desligado` |
| `TrocaDireta` | `true` | troca sem abrir a janela |
| `PreCarregarJanela` | `true` | aprende os botões de forma invisível ao entrar no servidor |
| `IntervaloMinimo` | `1.2` | segundos mínimos entre duas trocas |
| `BotaoDireito` | `true` | permite trocar com o botão direito |
| `DeslocamentoX` / `DeslocamentoY` | `8` / `42` | posição da barra em relação ao botão original |
| `TamanhoBotao` | `44` | tamanho de cada bola |
| `EspacoAutomatico` | `true` | abre espaço para as auras não se sobreporem (`false` usa só o `Espacamento`) |
| `QuadrosJoiaAtiva` / `QuadrosJoiasEncaixadas` | `40` / `15` | limite de quadros das animações (`0` = sem limite) |
| `ForcarNivel` | `-1` | `0`–`7` força um nível para ver o visual (`-1` = tempo real) |
| `HorasNiveis` | `1,2,3,4,5,20,50` | horas para cada um dos 7 níveis |
| `MostrarProgresso` | `true` | anel de progresso em volta das bolas |

## Compilando

```bash
dotnet build -c Release
```

Por padrão as referências vêm do NuGet (`BepInEx.Unity.IL2CPP`, `VRising.Unhollowed.Client`). Para compilar contra as DLLs do seu próprio jogo, crie `libs/core` (cópia de `BepInEx/core`) e `libs/interop` (cópia de `BepInEx/interop`). A pasta `libs/` nunca deve ir para o repositório.

Toda a arte é gerada por código em `src/GemArt.cs`. O projeto em `tools/preview` roda o mesmo código fora do jogo para gerar as prévias.

## Licença

[MIT](LICENSE). Mod feito por jogador, sem vínculo oficial com a Stunlock Studios ou com a equipe do Sangria Falls.
