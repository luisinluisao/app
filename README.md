# Slyth Optimizer

Otimizador de Windows em **um clique**, com interface preta e branca feita do zero em WPF (.NET 8).
Tudo o que ele muda é salvo antes e pode ser desfeito com um botão.

## Telas

- **Painel** — anel de pontuação animado (0–100), CPU e memória ao vivo com minigráficos, disco,
  processos, programas na inicialização e o botão **Otimizar** com três modos: **Seguro**, **Gamer** e **Extremo**.
- **Otimizações** — os 71 ajustes com busca, filtro por categoria e interruptor em cada um.
- **Rede** — teste de ping, variação (jitter) e perda de pacotes com gráfico ao vivo, e comparação
  real de velocidade entre DNS (operadora, Cloudflare, Google, Quad9, OpenDNS) com troca em um clique.
- **Inicialização** — programas que abrem com o Windows, com editor e local, para ligar/desligar
  (mesmo mecanismo do Gerenciador de Tarefas; nada é apagado).
- **BIOS e hardware** — ficha técnica do PC e recomendações de BIOS com o caminho exato no menu
  da sua placa-mãe (ASUS, MSI, Gigabyte, ASRock, Dell, HP, Lenovo, Acer) + botão *Reiniciar direto na BIOS*.
- **Histórico** — linha do tempo de cada otimização com botão *Desfazer*.

## Otimizações (71)

| Categoria | O que faz |
|---|---|
| **Desempenho** (16) | Plano de alto desempenho, prioridade MMCSS, inicialização sem atraso, menos escrita NTFS, pastas abrindo na hora, Sensor de Armazenamento, prioridade para o programa ativo, Power Throttling, USB sem suspensão, navegadores sem rodar escondidos, apps em segundo plano, desligamento rápido, cache NTFS maior, armazenamento reservado, hibernação, nomes 8.3 |
| **Jogos** (10) | Modo de Jogo, Game DVR, agendamento de GPU, jogos em janela, pop-ups da Game Bar, som que não abaixa sozinho, sem Teclas de Aderência, Windows Update sem trocar driver de vídeo, correção de MPO, tela cheia exclusiva |
| **Rede** (5) | Upload P2P de atualizações, limpar DNS, limitação de rede, Nagle (latência), placa de rede sem economia de energia |
| **Privacidade** (11) | Telemetria, tarefas agendadas escondidas, ID de anúncios, propagandas, Bing no Iniciar, histórico de atividades, feedback, notificações de dicas, Recall, Copilot, Widgets |
| **Serviços** (14) | Demonstração de loja, registro remoto, mapas, relatório de erros, fax, telemetria NVIDIA, rastreamento de links, compartilhamento de mídia, Insider, assistente de compatibilidade, SysMain, indexação, impressão, Xbox |
| **Limpeza** (10) | Temporários, cache do Windows Update, otimização de entrega, dumps de erro, navegadores, Discord/Steam/Spotify, cache de shaders, Lixeira, WinSxS, TRIM/desfragmentação |
| **Visual e entrada** (5) | Menus instantâneos, aceleração do mouse, teclado responsivo, efeitos visuais, transparência |

O Slyth não inclui ajustes que desligam proteções de segurança do Windows nem "tweaks" sem efeito comprovado.

Cada ajuste tem um nível; cada modo inclui o anterior:

- **Seguro** — sem efeito colateral, bom para qualquer PC.
- **Gamer** — + jogos, rede, entrada, serviços pouco usados e caches.
- **Extremo** — + indexação, SysMain, hibernação, efeitos visuais e limpeza profunda.
- **Manual** — nunca marcado automaticamente (MPO, impressora, Xbox): só se o usuário escolher.

Ao otimizar, o Slyth cria um ponto de restauração do Windows e salva o valor anterior de cada
configuração em `%ProgramData%\SlythOptimizer\Backups`. Se um ajuste falhar, ele é desfeito sozinho.

> **Por que a BIOS não é alterada automaticamente?** Não existe um jeito padrão de mudar a BIOS de
> todas as placas-mãe pelo Windows, e uma configuração errada pode fazer o PC não ligar.

## Estrutura

```
src/Slyth.Core       lógica: ajustes, backup, análise da BIOS (testável fora do Windows)
  Tweaks/TweakCatalog.cs   ← lista de todas as otimizações
src/Slyth.App        interface WPF
  Themes/Slyth.xaml        ← cores, tipografia e todos os controles
  Controls/                ← anel de pontuação e minigráfico desenhados à mão
  Views/                   ← Painel, Otimizações, BIOS, Histórico
tests/Slyth.Core.Tests
```

Para adicionar uma otimização, crie um item em `TweakCatalog.cs` — backup, "desfazer", filtros,
modos e pontuação passam a funcionar automaticamente.

## Como compilar

Precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) no Windows 10/11.

```powershell
dotnet test
dotnet run --project src/Slyth.App

# gerar um único SlythOptimizer.exe
dotnet publish src/Slyth.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

A cada push, o GitHub Actions gera o `SlythOptimizer.exe` em **Actions → build → Artifacts**.

## Antes de vender

- [ ] **Assinatura de código** — sem certificado, o SmartScreen e antivírus alertam quem baixar.
- [ ] **Licenciamento/pagamento** (ex.: Gumroad, Lemon Squeezy, Stripe).
- [ ] **Testar em PCs reais** (Windows 10 e 11, desktop e notebook, Intel e AMD).
- [ ] **Termos de uso e política de privacidade**.
- [ ] Não prometer números que o app não entrega ("+200% FPS") — gera reembolso e pode ser
      propaganda enganosa pelo Código de Defesa do Consumidor.
