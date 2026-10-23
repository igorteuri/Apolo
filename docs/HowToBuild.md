# Como compilar o Apolo

O Apolo é um programa de Windows (C#, **.NET 8**, WinForms, plataforma x86). O projeto herdou do Ares os nomes internos `Ares.*`, que continuam nas pastas e nos namespaces.

## Pelo GitHub (o jeito mais simples)

O arquivo `.github/workflows/build.yml` compila tudo a cada envio (`push`): publica o programa (`dotnet publish`, autocontido, win-x86), compila o pequeno ajudante de firewall (`Ares.WinSecurity`, .NET Framework 4.8), junta as bibliotecas nativas, empacota a ponte do Discord, o plugin do Stream Deck e o módulo do Foundry VTT, **roda os testes automáticos** e, só se eles passarem, gera o instalador (Inno Setup). O resultado aparece na aba *Actions*: `Apolo-installer` e `Apolo-windows`.

## No seu computador

1. Instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Rode os testes: `dotnet test tests/Apolo.Tests`.
3. Gere o programa: `dotnet publish src/Ares.Player/Ares.Player.csproj -c Release -r win-x86 --self-contained true -o dist`.
4. Copie para `dist` as bibliotecas nativas da pasta `Libraries` (`chromaprint.dll`, `avcodec-54.dll`, `avutil-52.dll`, `msvcp100.dll`, `msvcr100.dll`).
5. O ajudante de firewall precisa do MSBuild (Visual Studio ou Build Tools): `msbuild tools/WinSecurity.slnf /p:Configuration=Release /p:Platform=x86`.

## Os projetos (`src/*/*.csproj`)

São projetos .NET 8 no formato moderno (SDK): **todo `.cs` e todo `.resx` da pasta entra sozinho**, então criar, mover ou apagar um arquivo não exige mexer em nenhum `.csproj`. Só são listados à mão os arquivos que não são código (por exemplo, o HTML do overlay em `Ares.Player.csproj`) e os pacotes NuGet. Configurações comuns (avisos, idiomas) ficam em `Directory.Build.props`, na raiz.

- Bibliotecas portáteis (`net8.0`): `Ares.Data`, `Ares.Settings`, `Ares.Tags`, `Ares.ModelInfo` (pasta `Ares.ModelChecks`), `Ares.Playing`, `Ares.Players`, `Ares.Ipc`, `Ares.TagsImport`. **Não use WinForms nelas**: o compilador recusa.
- Só de Windows (`net8.0-windows`): `Ares.CommonGUI` e `Ares.Player` (o programa de Windows, WinForms).
- `Ares.AppCore` (`net8.0`): o que não depende de janela e serve às duas versões (preferências, árvore de pastas, enfeites dos botões).
- `Apolo.Desktop` (`net8.0`): a versão para Linux, macOS e Windows com Avalonia (`docs/LINUX-MAC.md`). Os GitHub Actions `portable.yml` e `desktop.yml` constroem e testam em Linux e macOS.
- `Ares.WinSecurity` é o único que continua em .NET Framework 4.8 (usa o COM do firewall); compila as poucas fontes de `Ares.Settings` e `Ares.Ipc` de que precisa.
- Se o arquivo tem lógica que dá para testar sem janela, ligue-o também em `tests/Apolo.Tests/Apolo.Tests.csproj`.

Ver também `docs/PORTABILIDADE.md` (o que falta para Mac e Linux).

## Observações

- A biblioteca BASS (que não é gratuita para uso comercial) **não** faz parte do Apolo e o workflow a remove da saída.
- O instalador é feito com o [Inno Setup](https://jrsoftware.org/isinfo.php) a partir de `Setup/Apolo.iss`. Para gerar na sua máquina, junte o resultado do build na pasta `dist` e rode `iscc /DMyAppVersion=0.81.0 Setup\Apolo.iss`.
- A assinatura digital (pasta `CodeSigning`) é opcional.
