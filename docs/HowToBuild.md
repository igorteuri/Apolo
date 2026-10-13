# Como compilar o Apolo

O Apolo é um programa de Windows (C#, .NET Framework 4.8, WinForms, plataforma x86). O projeto herdou do Ares os nomes internos `Ares.*`, que continuam nas pastas e nos namespaces.

## Pelo GitHub (o jeito mais simples)

O arquivo `.github/workflows/build.yml` compila tudo a cada envio (`push`). Ele baixa as bibliotecas necessárias (NAudio, NLayer, NVorbis, Concentus, TagLib#, WebView2 e cloudflared), compila o `Ares.sln`, empacota a ponte do Discord, o plugin do Stream Deck e o módulo do Foundry VTT, e publica o resultado como artefato **Apolo-windows** na aba *Actions*.

## No seu computador

1. Instale o Visual Studio (Community) com a carga de trabalho **Desenvolvimento para desktop com .NET**.
2. Coloque na pasta `Libraries` as bibliotecas que o workflow baixa (veja os passos "Download ... libraries" em `.github/workflows/build.yml`) e crie `src/Ares.AudioSource.Freesound/ApiKey.cs` a partir de `ApiKey.example.cs`.
3. Abra `Ares.sln`, escolha **Release** e **x86** e compile. A saída fica em `src\bin\Release`.
4. Copie o conteúdo de `Libraries` para a pasta de saída, para o programa rodar.

## Observações

- A biblioteca BASS (que não é gratuita para uso comercial) **não** faz parte do Apolo e o workflow a remove da saída.
- O script do instalador (`Setup/Ares_Setup.iss`, Inno Setup) ainda é o do Ares e precisa ser refeito para o Apolo.
- A assinatura digital (pasta `CodeSigning`) é opcional. Sem certificado, o projeto `Ares.WinSecurity` compila normalmente.
