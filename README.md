# PC Optimizer

Otimizador de Windows em **um clique**, feito para ser seguro o suficiente para vender:
tudo o que ele muda é salvo antes e pode ser desfeito com um botão.

## O que ele faz

**Botão "Otimizar agora"**

1. Cria um ponto de restauração do Windows.
2. Salva o valor anterior de cada configuração que vai mudar (em `%ProgramData%\PcOptimizer\Backups`, um JSON por execução).
3. Aplica os ajustes marcados (os recomendados já vêm marcados).
4. Mostra um relatório. Se um ajuste falhar, ele é desfeito sozinho e os outros continuam.

| Ajuste | Recomendado | Reversível |
|---|---|---|
| Plano de energia alto desempenho | ✔ | ✔ |
| Modo de Jogo do Windows | ✔ | ✔ |
| Desligar gravação em segundo plano (Game DVR) | ✔ | ✔ |
| Agendamento de GPU acelerado por hardware | ✔ | ✔ (requer reiniciar) |
| Prioridade para jogos e multimídia (MMCSS) | ✔ | ✔ |
| Reduzir telemetria (serviço DiagTrack) | ✔ | ✔ |
| Limpar arquivos temporários | ✔ | não precisa |
| Bloquear apps da Loja em segundo plano | | ✔ |
| Efeitos visuais para desempenho | | ✔ |
| Desligar aceleração do mouse | | ✔ |

Os ajustes ficam em `src/PcOptimizer.Core/Tweaks/TweakCatalog.cs`. Para criar um novo ajuste de registro,
basta adicionar um item à lista — backup e "desfazer" já funcionam automaticamente.

**Aba BIOS** — lê placa-mãe, BIOS, memória e placa de vídeo e diz, com o caminho do menu certo para
ASUS, MSI, Gigabyte, ASRock, Dell, HP, Lenovo e Acer:

- se o perfil de memória **XMP/EXPO** está desligado (costuma ser o maior ganho "escondido" de um PC gamer);
- se a memória está em **single channel**;
- se a **BIOS está desatualizada** (mais de 2 anos);
- se vale ativar **Resizable BAR** (RTX 30/40/50, RX 6000+, Intel Arc);
- se o Windows está em modo legado (CSM) ou com virtualização desligada.

Tem também o botão **"Reiniciar na BIOS"**, que reinicia o PC direto na tela da BIOS.

> **Por que a BIOS não é alterada automaticamente?** Não existe um jeito padrão de mudar a BIOS de
> todas as placas-mãe pelo Windows, e uma configuração errada (ex.: XMP instável) pode fazer o PC não
> ligar. Um app vendido que faz isso gera reembolsos, reclamações e responsabilidade. O app mostra o
> que mudar e onde — o usuário aplica em 1 minuto.

**Aba Histórico** — lista cada otimização com botão "Desfazer".

## Estrutura

```
src/PcOptimizer.Core     lógica (ajustes, backup, análise da BIOS) — testável fora do Windows
src/PcOptimizer.App      interface WPF (.NET 8), pede administrador ao abrir
tests/                   testes automáticos (xUnit)
```

## Como compilar

Precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) no Windows.

```powershell
dotnet test                       # roda os testes
dotnet run --project src/PcOptimizer.App

# gerar um único PcOptimizer.exe para distribuir
dotnet publish src/PcOptimizer.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

A cada push, o GitHub Actions (`.github/workflows/build.yml`) roda os testes e gera o `PcOptimizer.exe`
na aba **Actions → build → Artifacts**.

## Antes de vender

- [ ] **Nome e marca** próprios (troque "PC Optimizer" em `PcOptimizer.App.csproj` e `MainWindow.xaml`) e um ícone.
- [ ] **Assinatura de código** (certificado Code Signing). Sem isso o Windows SmartScreen e antivírus vão
      alertar quem baixar — é o maior motivo de desconfiança nesse tipo de app.
- [ ] **Licenciamento/pagamento** (ex.: chaves de licença via Gumroad, Lemon Squeezy ou Stripe).
- [ ] **Testar em PCs reais** (Windows 10 e 11, desktop e notebook, Intel e AMD).
- [ ] **Termos de uso e política de privacidade**.
- [ ] Não prometer números que o app não entrega ("+200% FPS"): além de gerar reembolsos, pode ser
      propaganda enganosa pelo Código de Defesa do Consumidor. Mostrar o antes/depois real é mais convincente.
