# Como compilar o Apolo

O Apolo é um programa de Windows (C#, **.NET 8**, WinForms, plataforma x86). O projeto herdou do Ares os nomes internos `Ares.*`, que continuam nas pastas e nos namespaces.

## Pelo GitHub (o jeito mais simples)

O arquivo `.github/workflows/build.yml` compila tudo a cada envio (`push`): publica o programa (`dotnet publish`, autocontido, win-x86), compila o pequeno ajudante de firewall (`Ares.WinSecurity`, .NET Framework 4.8), junta as bibliotecas nativas, empacota a ponte do Discord, o plugin do Stream Deck e o módulo do Foundry VTT, **roda os testes automáticos** e, só se eles passarem, gera o instalador (Inno Setup). O resultado aparece na aba *Actions*: `Apolo-installer` e `Apolo-windows`.

## No seu computador

1. Instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Rode os testes: `dotnet test tests/Apolo.Tests`.
3. Gere o programa: `dotnet publish net8/Ares.Player.Net8.csproj -c Release -r win-x86 --self-contained true -o dist`.
4. Copie para `dist` as bibliotecas nativas da pasta `Libraries` (`chromaprint.dll`, `avcodec-54.dll`, `avutil-52.dll`, `msvcp100.dll`, `msvcr100.dll`).
5. O ajudante de firewall precisa do MSBuild (Visual Studio ou Build Tools): `msbuild tools/WinSecurity.slnf /p:Configuration=Release /p:Platform=x86`.

## Os projetos `net8/*.csproj`

Eles são **gerados** por `python tools/gen_net8.py` a partir da lista de arquivos dos projetos antigos (`src/*/*.csproj`). Ao criar, mover ou apagar um arquivo `.cs`, edite o `.csproj` de `src` e rode o gerador (ou edite o de `net8` igual). Se o arquivo tem lógica que dá para testar sem janela, ligue-o também em `tests/Apolo.Tests/Apolo.Tests.csproj`.

## Observações

- A biblioteca BASS (que não é gratuita para uso comercial) **não** faz parte do Apolo e o workflow a remove da saída.
- O instalador é feito com o [Inno Setup](https://jrsoftware.org/isinfo.php) a partir de `Setup/Apolo.iss`. Para gerar na sua máquina, junte o resultado do build na pasta `dist` e rode `iscc /DMyAppVersion=0.81.0 Setup\Apolo.iss`.
- A assinatura digital (pasta `CodeSigning`) é opcional.
