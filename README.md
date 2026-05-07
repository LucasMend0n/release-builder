# ReleaseBuilder

Console application que automatiza o processo de checkout e rebuild de múltiplos repositórios .NET para uma branch de release específica.

## O que faz?

O ReleaseBuilder percorre uma lista ordenada de repositórios, e para cada um:

1. Verifica se há alterações locais não commitadas
2. Executa `git fetch origin`
3. Faz checkout para `release/{versão}`
4. Puxa as últimas alterações da branch
5. Executa `dotnet clean` + `dotnet build --no-incremental` (rebuild completo)

Ao final, exibe um relatório com o status e o tempo de cada repositório.

---

## Instalação

### Opção 1 — Baixar o executável (recomendado)

Acesse a página de [Releases](../../releases) e baixe o `ReleaseBuilder.exe` da versão mais recente. Não é necessário ter o SDK do .NET instalado.

### Opção 2 — Compilar a partir do código-fonte

Requer o [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/seu-usuario/ReleaseBuilder.git
cd ReleaseBuilder
dotnet build -c Release
```

---

## Configuração

1. Copie o arquivo de exemplo:

```bash
cp repos-config.example.json appsettings.json
```

2. Edite o `repos-config.json` com os seus repositórios:

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
| `repositories[].name` | Nome da pasta do repositório dentro de `rootPath` |
| `repositories[].solutionFile` | Nome do arquivo `.sln` dentro do repositório |

> **Importante:** a ordem dos repositórios importa. Coloque bibliotecas e dependências antes dos projetos que as consomem.

---

## Uso

```bash
ReleaseBuilder.exe --version 1.5.0
```

Formas aceitas:

```bash
ReleaseBuilder.exe --version 1.5.0
ReleaseBuilder.exe -v 1.5.0
ReleaseBuilder.exe 1.5.0
```

O programa vai fazer checkout para a branch `release/1.5.0` em todos os repositórios configurados e rebuildar cada solution.

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
[INFO] Config loaded from: C:\Tools\repos-config.json

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

## Publicação

Para gerar um executável único e distribuir para o time:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

O executável será gerado em `bin/Release/net8.0/win-x64/publish/`.

Runtimes disponíveis: `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, `osx-arm64`.

---

## Estrutura do projeto

```
ReleaseBuilder/
├── Program.cs                  # Entry point e orquestração
├── ReleaseBuilder.csproj       # Projeto .NET 8
├── repos-config.json           # Configuração (não versionar)
├── repos-config.example.json   # Template de configuração
├── Models/
│   ├── BuildConfig.cs          # Modelo do JSON de configuração
│   └── BuildResult.cs          # Resultado de cada repositório
└── Services/
    ├── GitService.cs           # Operações git (fetch, checkout, pull)
    ├── BuildService.cs         # dotnet clean + build
    └── ConsoleLogger.cs        # Saída colorida no terminal
```

---

## Pré-requisitos

- **Git** instalado e disponível no PATH
- **Repositórios já clonados** na pasta definida em `rootPath`
- Para compilar: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Para usar o executável publicado: nenhum (self-contained)