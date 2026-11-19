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
- **Joias de cristal**: cada Corrente é uma bola com uma joia encaixada. Quando ativa, a joia se solta deixando o buraco na bola, e surge a aura do elemento.

| Corrente | Joia | Aura |
|---|---|---|
| Glacial | floco de neve | halo gelado, cristais e flocos caindo |
| Caos | caveira | fogo roxo e brasas |
| Tempestade | raio amarelo | arcos elétricos |
| Corrompida | olho com pupila em fenda | corrupção líquida e bolhas |
| Luminosa | sol | halo pulsante e raios de luz |
| Sanguínea | gota de sangue | névoa de sangue e gotas pingando |

## Evolução por tempo de uso

Cada Corrente evolui conforme o tempo que você passa com ela ativa (pausa após 5 min sem mexer o mouse/teclado):

| Nível | Tempo | Visual |
|---|---|---|
| Bruta | 0 h | joia lapidada simples |
| Lapidada | 5 h | lapidação fina e aro de prata |
| Lustrosa | 20 h | aro de ouro com gemas, aura mais intensa |
| Lendária | 50 h | joia com luz própria, fragmentos orbitando e um traço temático na aura |

<p align="center"><img src="docs/niveis.png" alt="Os quatro níveis de cada Corrente"></p>

Um anel fino em volta de cada bola mostra o progresso até o próximo nível. O tempo fica salvo só no seu PC, em `BepInEx/config/CorrenteRapida_tempo.txt`.

## Instalação

1. Tenha o **BepInEx 6 (IL2CPP)** e o **ScarletInterface** (já vêm com o launcher do Sangria Falls).
2. Baixe o `CorrenteRapida.dll` na página de [Releases](../../releases) e coloque em `BepInEx/plugins`.
3. Entre no servidor e abra a janela "Trocar de Corrente" uma vez pelo botão normal: o mod aprende onde ele fica e a barra aparece.

## Configuração

Arquivo `BepInEx/config/sangria.correnterapida.cfg` (criado na primeira execução). Destaques:

| Opção | Padrão | O que faz |
|---|---|---|
| `TrocaDireta` | `true` | troca sem abrir a janela |
| `PreCarregarJanela` | `true` | aprende os botões de forma invisível ao entrar no servidor |
| `IntervaloMinimo` | `1.2` | segundos mínimos entre duas trocas |
| `BotaoDireito` | `true` | permite trocar com o botão direito |
| `DeslocamentoX` / `DeslocamentoY` | `8` / `0` | posição da barra em relação ao botão original |
| `TamanhoBotao` | `34` | tamanho de cada bola |
| `ForcarNivel` | `-1` | `0`–`3` força um nível para ver o visual (`-1` = tempo real) |
| `HorasNiveis` | `5,20,50` | horas para Lapidada, Lustrosa e Lendária |
| `MostrarProgresso` | `true` | anel de progresso em volta das bolas |

## Compilando

```bash
dotnet build -c Release
```

Por padrão as referências vêm do NuGet (`BepInEx.Unity.IL2CPP`, `VRising.Unhollowed.Client`). Para compilar contra as DLLs do seu próprio jogo, crie `libs/core` (cópia de `BepInEx/core`) e `libs/interop` (cópia de `BepInEx/interop`). A pasta `libs/` nunca deve ir para o repositório.

Toda a arte é gerada por código em `src/GemArt.cs`. O projeto em `tools/preview` roda o mesmo código fora do jogo para gerar as prévias.

## Licença

[MIT](LICENSE). Mod feito por jogador, sem vínculo oficial com a Stunlock Studios ou com a equipe do Sangria Falls.
