# ReleaseBuilder

Console application que automatiza o processo de checkout e rebuild de múltiplos repositórios .NET para uma branch de release específica.

## O que faz?

O ReleaseBuilder percorre uma lista ordenada de repositórios, e para cada um:

1. Verifica se há alterações locais não commitadas (e faz `git stash` se houver)
2. Executa `git fetch origin`
3. Faz checkout para `release/{versão}` (cria a branch local a partir de `origin/release/{versão}` se necessário)
4. Puxa as últimas alterações da branch
5. Executa `dotnet restore` + `dotnet clean` + `dotnet build --no-incremental` (rebuild completo)

Ao final, exibe um relatório com o status e o tempo de cada repositório.

---

## Instalação (Windows)

1. Vá em [Releases](../../releases) e baixe o arquivo `release-builder-vX.Y.Z-win-x64.zip` da versão mais recente.
2. Extraia o ZIP onde preferir (a pasta `Downloads` serve — pode apagar depois).
3. Dê duplo clique em `install.bat` (ou rode no terminal). O script:
   - Copia o executável para `%LOCALAPPDATA%\Programs\release-builder\`
   - Adiciona essa pasta ao seu `PATH` (sem precisar de admin)
   - Cria `%APPDATA%\release-builder\appsettings.json` a partir do template (se ainda não existir)
4. **Reabra o terminal** para o `PATH` ser reconhecido.

Não é necessário ter .NET SDK ou runtime instalado — o executável é self-contained.

### Atualização

Baixe o ZIP da nova versão e rode o `install.bat` de novo. O executável é sobrescrito e seu `appsettings.json` é preservado.

### Desinstalação

Rode o `uninstall.bat` que veio no ZIP. Ele remove o executável e tira a pasta do `PATH`. Sua config em `%APPDATA%\release-builder\` é preservada — apague manualmente se quiser limpar tudo.

---

## Configuração

A config fica em `%APPDATA%\release-builder\appsettings.json`. Pra descobrir o caminho exato a qualquer momento:

```bash
release-builder --config-path
```

Edite com os seus repositórios:

```json
{
  "rootPath": "C:\\Repos",
  "stopOnError": false,
  "repositories": [
    {
      "name": "Core.Library",
      "solutionFile": "Core.Library.sln"
    },
    {
      "name": "Shared.Services",
      "solutionFile": "Shared.Services.sln"
    },
    {
      "name": "Main.WebApp",
      "solutionFile": "Main.WebApp.sln"
    }
  ]
}
```

| Campo | Descrição |
|---|---|
| `rootPath` | Pasta raiz onde estão os repositórios clonados |
| `stopOnError` | Se `true`, para no primeiro erro. Se `false`, continua e reporta tudo no final |
| `repositories` | Lista ordenada — a ordem define a sequência de build |
| `repositories[].name` | Pasta do repositório dentro de `rootPath` (pode conter subcaminhos) |
| `repositories[].solutionFile` | `.sln` ou `.csproj` dentro do repositório |

> **Importante:** a ordem dos repositórios importa. Coloque bibliotecas e dependências antes dos projetos que as consomem.

Veja exemplos reais em [`examples/`](./examples/).

---

## Uso

```bash
release-builder --version 1.5.0
```

Formas aceitas:

```bash
release-builder --version 1.5.0
release-builder -v 1.5.0
release-builder 1.5.0
```

### Flags

| Flag | Descrição |
|---|---|
| `-v`, `--version <versão>` | Branch alvo será `release/<versão>` |
| `-c`, `--config <caminho>` | Usa um arquivo de config alternativo (útil para testes) |
| `--config-path` | Imprime o caminho padrão da config e sai |

---

## Exemplo de saída

```
════════════════════════════════════════════════════════════
  Release Builder — version 1.5.0
════════════════════════════════════════════════════════════
[INFO] Root path: C:\Repos
[INFO] Repositories: 3
[INFO] Target branch: release/1.5.0
[INFO] Stop on error: False
[INFO] Config loaded from: C:\Users\jojos\AppData\Roaming\release-builder\appsettings.json

── [1/3] Core.Library ──
[INFO] Checking working tree status...
[INFO] Fetching from origin...
[INFO] Checking out release/1.5.0...
[INFO] Pulling latest changes...
[OK]   Git operations completed
[INFO] Rebuilding Core.Library.sln...
[OK]   Build succeeded

── [2/3] Shared.Services ──
...

════════════════════════════════════════════════════════════
  Build report
════════════════════════════════════════════════════════════
[OK]   Core.Library                      12.3s
[OK]   Shared.Services                    8.7s
[FAIL] Main.WebApp                        3.1s
[FAIL]   └─ Checkout failed: branch 'release/1.5.0' not found

Total: 3 | Succeeded: 2 | Failed: 1
Total time: 24.1s
```

---

## Pré-requisitos

- **Git** instalado e disponível no `PATH`
- **.NET SDK** instalado e disponível no `PATH` (necessário para o `dotnet build` rodar nos repositórios — não para o ReleaseBuilder em si)
- **Repositórios já clonados** na pasta definida em `rootPath`

---

## Compilando a partir do código-fonte

Requer [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/<seu-usuario>/release-builder.git
cd release-builder
dotnet build -c Release
```

Para gerar o executável single-file self-contained:

```bash
dotnet publish -c Release -r win-x64
```

O `.exe` é gerado em `bin/Release/net10.0/win-x64/publish/`.

---

## Estrutura do projeto

```
release-builder/
├── Program.cs                       # Entry point + orquestração
├── release-builder.csproj           # Projeto .NET 10
├── Model/
│   ├── BuildConfig.cs               # Modelo do JSON de configuração
│   └── BuildResult.cs               # Resultado de cada repositório
├── Services/
│   ├── GitService.cs                # Operações git (fetch, checkout, pull, stash)
│   ├── BuildService.cs              # dotnet restore + clean + build
│   └── Logger.cs                    # Saída colorida no terminal
├── examples/                        # Configurações de exemplo
├── installer/
│   ├── install.bat                  # Instalador (incluído no ZIP de release)
│   └── uninstall.bat                # Desinstalador
└── .github/workflows/
    ├── ci.yml                       # Lint + build em PRs para main
    └── release.yml                  # Publica exe + ZIP quando uma tag v* é criada
```
