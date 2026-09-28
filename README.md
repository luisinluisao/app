# Slyth Optimizer

Otimizador de Windows em **um clique**, com interface preta e branca feita do zero em WPF (.NET 8).
Tudo o que ele muda é salvo antes e pode ser desfeito com um botão.

## Telas

- **Painel** — anel de pontuação animado (0–100) com base no que já está otimizado, CPU e memória
  ao vivo com minigráficos, disco, processos e o botão **Otimizar** com três modos:
  **Seguro**, **Gamer** e **Extremo**.
- **Otimizações** — os 54 ajustes com busca, filtro por categoria e interruptor em cada um.
- **BIOS e hardware** — ficha técnica do PC e recomendações de BIOS com o caminho exato no menu
  da sua placa-mãe (ASUS, MSI, Gigabyte, ASRock, Dell, HP, Lenovo, Acer) + botão *Reiniciar direto na BIOS*.
- **Histórico** — linha do tempo de cada otimização com botão *Desfazer*.

## Otimizações (54)

| Categoria | O que faz |
|---|---|
| **Desempenho** (10) | Plano de alto desempenho, prioridade MMCSS, inicialização sem atraso, menos escrita NTFS, prioridade para o programa ativo, Power Throttling, apps em segundo plano, desligamento rápido, hibernação, nomes 8.3 |
| **Jogos** (6) | Modo de Jogo, Game DVR, agendamento de GPU por hardware, otimizações para jogos em janela, pop-ups da Game Bar, tela cheia exclusiva |
| **Rede** (5) | Upload P2P de atualizações, limpar DNS, limitação de rede, Nagle (latência), DNS Cloudflare |
| **Privacidade** (9) | Telemetria, ID de anúncios, propagandas e apps instalados sozinhos, Bing no Iniciar, histórico de atividades, feedback, Recall, Copilot, Widgets |
| **Serviços** (9) | Demonstração de loja, registro remoto, mapas, relatório de erros, fax, SysMain, indexação, spooler de impressão, Xbox |
| **Limpeza** (10) | Temporários, cache do Windows Update, otimização de entrega, dumps de erro, navegadores, Discord/Steam/Spotify, cache de shaders, Lixeira, WinSxS, TRIM/desfragmentação |
| **Visual e entrada** (5) | Menus instantâneos, aceleração do mouse, teclado responsivo, efeitos visuais, transparência |

Cada ajuste tem um nível; cada modo inclui o anterior:

- **Seguro** — sem efeito colateral, bom para qualquer PC.
- **Gamer** — + jogos, rede, entrada, serviços pouco usados e caches.
- **Extremo** — + indexação, SysMain, hibernação, efeitos visuais e limpeza profunda.
- **Manual** — nunca marcado automaticamente (DNS, impressora, Xbox): só se o usuário escolher.

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
