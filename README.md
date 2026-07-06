# Resolvia

Sistema de gestão de chamados (ticket management) construído para simular, na prática, o dia a dia de uma operação de suporte técnico / service desk: abertura e priorização de chamados, controle de SLA, histórico de auditoria, comentários entre cliente e atendente, e upload de anexos.

O projeto foi desenvolvido com foco em **Clean Architecture**, separando claramente regras de negócio, acesso a dados e exposição via API — a mesma abordagem usada em sistemas de produção reais.

---

## Índice

- [Stack utilizada](#stack-utilizada)
- [Arquitetura](#arquitetura)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Domain — Entidades e Enums](#domain--entidades-e-enums)
- [Application — DTOs, Interfaces e Services](#application--dtos-interfaces-e-services)
- [Infrastructure — Persistência e Storage](#infrastructure--persistência-e-storage)
- [API — Controllers e configuração](#api--controllers-e-configuração)
- [Regras de negócio principais](#regras-de-negócio-principais)
- [Autenticação e autorização](#autenticação-e-autorização)
- [Endpoints da API](#endpoints-da-api)
- [Como rodar localmente](#como-rodar-localmente)
- [Variáveis de ambiente](#variáveis-de-ambiente)

---

## Stack utilizada

| Tecnologia | Uso |
|---|---|
| C# / .NET 8 | Linguagem e runtime |
| ASP.NET Core | Framework Web API |
| Entity Framework Core | ORM |
| PostgreSQL | Banco de dados relacional |
| JWT (JSON Web Token) | Autenticação e autorização |
| BCrypt.Net-Next | Hash de senhas |
| Docker / Docker Compose | Containerização do banco local |
| Cloudflare R2 (S3-compatible) | Armazenamento de anexos na nuvem |
| AWSSDK.S3 | Cliente para comunicação com o R2 |
| Swagger / Swashbuckle | Documentação e teste interativo da API |
| Railway | Deploy em produção |

---

## Arquitetura

O projeto segue **Clean Architecture**, organizada em 4 camadas com dependência sempre "de fora pra dentro":

```
Resolvia.API  →  Resolvia.Infrastructure  →  Resolvia.Application  →  Resolvia.Domain
```

- **Domain**: o núcleo do sistema. Contém as entidades (`Ticket`, `User`, `Category`, etc.) e enums. Não depende de nenhuma outra camada, não conhece banco de dados, não conhece HTTP. É "puro" C#.
- **Application**: contém a lógica de negócio (Services), os contratos de persistência (Interfaces) e os DTOs que trafegam entre a API e o mundo externo. Conhece o Domain, mas nunca conhece Entity Framework, PostgreSQL ou ASP.NET diretamente.
- **Infrastructure**: implementa de fato as interfaces definidas no Application — é aqui que mora o Entity Framework Core, o `DbContext`, os Repositories reais, e a integração com o Cloudflare R2.
- **API**: a camada mais externa. Expõe os endpoints HTTP (Controllers), configura autenticação JWT, Swagger, injeção de dependência, e é o ponto de entrada da aplicação (`Program.cs`).

**Por que isso importa:** o `TicketService` (Application), por exemplo, nunca sabe que existe PostgreSQL ou EF Core — ele só conhece a interface `ITicketRepository`. Isso significa que trocar de banco de dados, ou de provedor de storage, não exige tocar em nenhuma regra de negócio — só na implementação da camada Infrastructure.

---

## Estrutura de pastas

```
Resolvia/
├── src/
│   ├── Resolvia.API/
│   │   ├── Controllers/
│   │   ├── Security/
│   │   ├── Properties/
│   │   ├── appsettings.json
│   │   └── Program.cs
│   ├── Resolvia.Application/
│   │   ├── DTOs/
│   │   │   ├── Auth/
│   │   │   ├── Category/
│   │   │   ├── Ticket/
│   │   │   ├── Comment/
│   │   │   └── Attachment/
│   │   ├── Interfaces/
│   │   └── Services/
│   ├── Resolvia.Domain/
│   │   ├── Entities/
│   │   └── Enums/
│   └── Resolvia.Infrastructure/
│       ├── Data/
│       │   └── Configurations/
│       ├── Repositories/
│       ├── Security/
│       └── Storage/
├── docker-compose.yml
└── README.md
```

---

## Domain — Entidades e Enums

Camada mais interna do projeto. Não depende de nada além do próprio C#.

### Enums

| Arquivo | Função | Quem depende dele |
|---|---|---|
| `Enums/UserRole.cs` | Define os papéis possíveis: `Cliente`, `Atendente`, `Admin` | `User.cs` usa esse enum |
| `Enums/TicketPriority.cs` | Define os níveis de prioridade do chamado: `Baixa`, `Media`, `Alta`, `Urgente` | `Ticket.cs` usa esse enum |
| `Enums/TicketStatus.cs` | Define os status possíveis: `Aberto`, `EmAndamento`, `AguardandoCliente`, `Resolvido`, `Fechado` | `Ticket.cs` usa esse enum |

### Entidades

| Arquivo | Função | Quem depende dele |
|---|---|---|
| `Entities/User.cs` | Representa um usuário do sistema (cliente, atendente ou admin) | Referenciado por `Ticket` (como `Requester` e `AssignedTo`) e por `TicketComment`, `TicketHistory` |
| `Entities/Category.cs` | Representa a categoria do chamado (ex: Hardware, Rede, Software) e o SLA padrão dela (`DefaultSlaHours`) | Referenciado por `Ticket` |
| `Entities/Ticket.cs` | Entidade central do sistema — o chamado em si. Conecta `User` (quem abriu, quem atende) e `Category`, e tem coleções de `Comments`, `Attachments` e `History` | Depende de `User` e `Category`; é referenciado por `TicketComment`, `TicketAttachment`, `TicketHistory` |
| `Entities/TicketComment.cs` | Representa um comentário/interação dentro do chamado (público ou nota interna via campo `IsInternal`) | Depende de `Ticket` e `User` |
| `Entities/TicketAttachment.cs` | Representa um arquivo anexado ao chamado (nome e URL do arquivo) | Depende de `Ticket` |
| `Entities/TicketHistory.cs` | Registra auditoria: quem mudou o quê, de qual valor pra qual valor, e quando | Depende de `Ticket` e `User` |

**Resumo do fluxo do Domain:** `Ticket` é o centro de tudo. Ele "puxa" `User` e `Category`, e é "puxado" por `TicketComment`, `TicketAttachment` e `TicketHistory`.

---

## Application — DTOs, Interfaces e Services

### DTOs de Auth

| Arquivo | Função |
|---|---|
| `DTOs/Auth/RegisterRequest.cs` | Corpo enviado ao se cadastrar (Name, Email, Password). Não tem campo `Role` — todo cadastro público vira `Cliente` por padrão |
| `DTOs/Auth/LoginRequest.cs` | Corpo enviado no login (Email, Password) |
| `DTOs/Auth/AuthResponse.cs` | Retorno após login/cadastro: token JWT + Name, Email e Role do usuário |

### DTOs de Category

| Arquivo | Função |
|---|---|
| `DTOs/Category/CategoryRequest.cs` | Corpo para criar/editar categoria (Name, Description, DefaultSlaHours) |
| `DTOs/Category/CategoryResponse.cs` | Retorno ao consultar categorias |

### DTOs de Ticket

| Arquivo | Função |
|---|---|
| `DTOs/Ticket/TicketCreateRequest.cs` | Corpo para abrir um chamado (Title, Description, CategoryId, Priority). Não tem `RequesterId` — o solicitante é sempre o usuário autenticado, extraído do token JWT |
| `DTOs/Ticket/TicketUpdateStatusRequest.cs` | Corpo para mudar o status de um chamado (NewStatus) |
| `DTOs/Ticket/TicketAssignRequest.cs` | Corpo para atribuir um chamado a um atendente (AtendenteId) |
| `DTOs/Ticket/TicketResponse.cs` | Retorno "achatado" do chamado — nomes em vez de Ids/objetos aninhados, incluindo `IsOverdue` calculado na hora da resposta |
| `DTOs/Ticket/TicketHistoryResponse.cs` | Retorno de uma entrada do histórico de auditoria (campo alterado, valor antigo, novo valor, quem alterou, quando) |

### DTOs de Comment

| Arquivo | Função |
|---|---|
| `DTOs/Comment/CommentCreateRequest.cs` | Corpo para criar um comentário (Message, IsInternal — default `false`) |
| `DTOs/Comment/CommentResponse.cs` | Retorno de um comentário, já com o nome do autor |

### DTOs de Attachment

| Arquivo | Função |
|---|---|
| `DTOs/Attachment/AttachmentResponse.cs` | Retorno de um anexo (Id, FileName, FileUrl, UploadedAt) |

### Interfaces (contratos)

| Arquivo | Função |
|---|---|
| `Interfaces/IUserRepository.cs` | Contrato de persistência de usuários (buscar por email, adicionar, salvar). Implementado de verdade no Infrastructure |
| `Interfaces/IPasswordHasher.cs` | Contrato para hash e verificação de senha, independente de biblioteca específica (BCrypt hoje, poderia ser Argon2 amanhã sem mudar o Service) |
| `Interfaces/ITokenService.cs` | Contrato para geração de JWT a partir de um `User` |
| `Interfaces/ICategoryRepository.cs` | Contrato de persistência de categorias (CRUD completo) |
| `Interfaces/ITicketRepository.cs` | Contrato de persistência de chamados — inclui métodos de Ticket, Histórico, Comentários e Anexos (todos vinculados ao chamado) |
| `Interfaces/IFileStorageService.cs` | Contrato genérico de armazenamento de arquivo ("salva em algum lugar, devolve a URL") — não sabe que por trás existe Cloudflare R2 |

### Services (regra de negócio)

| Arquivo | Função |
|---|---|
| `Services/AuthService.cs` | Regras de cadastro (bloqueia e-mail duplicado, força `Role: Cliente` em cadastros públicos) e login (verifica senha, gera token). Nunca conhece banco de dados ou JWT diretamente — só as interfaces |
| `Services/CategoryService.cs` | CRUD de categorias, convertendo entre `Category` (entidade) e os DTOs que trafegam pela API |
| `Services/TicketService.cs` | O coração do sistema. Contém: <br>• Cálculo automático de `DueDate` (SLA da categoria ajustado pela prioridade)<br>• Regra de visibilidade (`Cliente` só vê os próprios chamados; `Atendente`/`Admin` veem todos)<br>• Registro automático de histórico em toda mudança de status ou atribuição<br>• Regras de comentários (nota interna nunca visível/criável por `Cliente`)<br>• Upload de anexos com validação de tamanho máximo (5 MB) |

---

## Infrastructure — Persistência e Storage

### Data (Entity Framework Core)

| Arquivo | Função |
|---|---|
| `Data/ResolviaDbContext.cs` | Ponte entre as entidades do Domain e o PostgreSQL. Cada `DbSet<T>` vira uma tabela. Aplica automaticamente todas as `Configurations` via `ApplyConfigurationsFromAssembly` |
| `Data/Configurations/UserConfiguration.cs` | Regras da tabela `Users`: e-mail único, tamanho máximo de campos, `Role` salvo como texto |
| `Data/Configurations/CategoryConfiguration.cs` | Regras da tabela `Categories` |
| `Data/Configurations/TicketConfiguration.cs` | Relações de chave estrangeira do `Ticket` com `User` (Requester e AssignedTo) e `Category`, com `DeleteBehavior.Restrict` para nunca apagar um usuário/categoria em cascata junto com o chamado |
| `Data/Configurations/TicketCommentConfiguration.cs` | Comentário é apagado em cascata se o chamado for apagado, mas o usuário autor nunca é apagado |
| `Data/Configurations/TicketAttachmentConfiguration.cs` | Anexo é apagado em cascata se o chamado for apagado |
| `Data/Configurations/TicketHistoryConfiguration.cs` | Histórico é apagado em cascata se o chamado for apagado, mas o usuário que fez a mudança nunca é apagado |

### Repositories (implementação real dos contratos)

| Arquivo | Função |
|---|---|
| `Repositories/UserRepository.cs` | Implementa `IUserRepository` usando EF Core |
| `Repositories/CategoryRepository.cs` | Implementa `ICategoryRepository`. Usa `AsNoTracking()` em consultas de listagem para melhor performance |
| `Repositories/TicketRepository.cs` | Implementa `ITicketRepository`. Centraliza os `.Include()` de relacionamentos (Category, Requester, AssignedTo) em um método privado reaproveitado por todas as consultas |

### Security

| Arquivo | Função |
|---|---|
| `Security/PasswordHasher.cs` | Implementa `IPasswordHasher` usando a biblioteca BCrypt.Net-Next |

### Storage (Cloudflare R2)

| Arquivo | Função |
|---|---|
| `Storage/R2FileStorageService.cs` | Implementa `IFileStorageService` usando o SDK `AWSSDK.S3` apontando para o endpoint do Cloudflare R2 (compatível com a API do S3). Gera um GUID na frente do nome do arquivo para evitar colisões, e retorna a URL pública do arquivo enviado |

---

## API — Controllers e configuração

### Security

| Arquivo | Função |
|---|---|
| `Security/TokenService.cs` | Implementa `ITokenService`. Gera o JWT com claims de Id, Nome, Email e Role do usuário, usando a chave secreta e configurações lidas do `appsettings.json` |

### Controllers

| Arquivo | Função |
|---|---|
| `Controllers/AuthController.cs` | Expõe `POST /api/Auth/register` e `POST /api/Auth/login` |
| `Controllers/CategoryController.cs` | Expõe o CRUD de categorias. Leitura liberada para qualquer usuário autenticado; escrita restrita a `Admin` |
| `Controllers/TicketController.cs` | Expõe todos os endpoints de chamados: criação, consulta, mudança de status, atribuição, histórico, comentários e upload de anexos. Extrai `CurrentUserId` e `CurrentUserRole` diretamente das claims do token JWT |

### Program.cs

Ponto de entrada da aplicação. Responsável por:
- Registrar o `ResolviaDbContext` apontando para o PostgreSQL
- Configurar autenticação JWT (issuer, audience, chave de assinatura)
- Configurar o Swagger com suporte a autenticação Bearer (botão "Authorize")
- Registrar, via injeção de dependência, cada interface do Application com sua implementação real do Infrastructure/API

---

## Regras de negócio principais

### Cálculo de SLA

Ao criar um chamado, o prazo (`DueDate`) é calculado como:

```
DueDate = CreatedAt + (SLA da categoria × multiplicador da prioridade)
```

| Prioridade | Multiplicador |
|---|---|
| Urgente | 25% do SLA da categoria |
| Alta | 50% do SLA da categoria |
| Média | 100% do SLA da categoria |
| Baixa | 150% do SLA da categoria |

Exemplo: categoria "Hardware" com SLA de 24h + prioridade "Urgente" → prazo de 6 horas.

### Visibilidade de chamados

- **Cliente**: só enxerga os chamados que ele mesmo abriu
- **Atendente / Admin**: enxergam todos os chamados

### Auditoria automática

Toda mudança de `Status` ou de `AssignedTo` (atendente responsável) gera automaticamente uma entrada em `TicketHistory`, registrando valor antigo, novo valor, quem mudou e quando — sem exigir nenhuma ação extra de quem está usando a API.

### Comentários públicos vs. notas internas

- Qualquer comentário pode ser marcado como `IsInternal`
- **Cliente nunca pode criar nem visualizar notas internas** — mesmo que tente forçar `isInternal: true` na requisição, o backend ignora e força `false`
- Atendente e Admin veem e podem criar ambos os tipos

### Upload de anexos

- Limite de 5 MB por arquivo (validado no backend antes do upload)
- Armazenamento no Cloudflare R2, com URL pública de acesso direto
- Mesma regra de visibilidade dos chamados: Cliente só anexa/vê arquivos dos próprios chamados

---

## Autenticação e autorização

- Autenticação via **JWT Bearer Token**
- Papéis de usuário: `Cliente`, `Atendente`, `Admin`
- Endpoints protegidos com `[Authorize]`, com restrição adicional por papel via `[Authorize(Roles = "...")]` onde necessário

| Ação | Cliente | Atendente | Admin |
|---|---|---|---|
| Abrir chamado | ✅ | ✅ | ✅ |
| Ver próprios chamados | ✅ | ✅ | ✅ |
| Ver todos os chamados | ❌ | ✅ | ✅ |
| Mudar status / atribuir chamado | ❌ | ✅ | ✅ |
| Criar/editar/excluir categoria | ❌ | ❌ | ✅ |
| Criar nota interna | ❌ | ✅ | ✅ |

---

## Endpoints da API

### Auth
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/Auth/register` | Cadastro de novo usuário (sempre como Cliente) |
| POST | `/api/Auth/login` | Login, retorna token JWT |

### Category
| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| GET | `/api/Category` | Lista todas as categorias | Autenticado |
| GET | `/api/Category/{id}` | Consulta uma categoria | Autenticado |
| POST | `/api/Category` | Cria categoria | Admin |
| PUT | `/api/Category/{id}` | Edita categoria | Admin |
| DELETE | `/api/Category/{id}` | Remove categoria | Admin |

### Ticket
| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| GET | `/api/Ticket` | Lista chamados visíveis para o usuário | Autenticado |
| GET | `/api/Ticket/{id}` | Consulta um chamado | Autenticado (dono ou Atendente/Admin) |
| POST | `/api/Ticket` | Abre um novo chamado | Autenticado |
| PATCH | `/api/Ticket/{id}/status` | Muda o status do chamado | Atendente, Admin |
| PATCH | `/api/Ticket/{id}/assign` | Atribui o chamado a um atendente | Atendente, Admin |
| GET | `/api/Ticket/{id}/history` | Consulta o histórico de auditoria | Autenticado (dono ou Atendente/Admin) |
| POST | `/api/Ticket/{id}/comments` | Adiciona um comentário | Autenticado (dono ou Atendente/Admin) |
| GET | `/api/Ticket/{id}/comments` | Lista comentários do chamado | Autenticado (dono ou Atendente/Admin) |
| POST | `/api/Ticket/{id}/attachments` | Envia um anexo (máx. 5 MB) | Autenticado (dono ou Atendente/Admin) |

---

## Como rodar localmente

### Pré-requisitos
- .NET SDK 8+
- Docker Desktop
- Conta no Cloudflare (para o R2, se for testar upload de anexos)

### Passo a passo

```bash
# 1. Subir o PostgreSQL local via Docker
docker compose up -d

# 2. Restaurar pacotes e aplicar as migrations
dotnet restore
dotnet ef database update --project src/Resolvia.Infrastructure --startup-project src/Resolvia.API

# 3. Rodar a API
dotnet run --project src/Resolvia.API
```

A API sobe em `https://localhost:7241` (ou porta configurada em `launchSettings.json`), com Swagger disponível em `/swagger`.

---

## Variáveis de ambiente

Configuração local em `src/Resolvia.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=resolvia;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "SUA_CHAVE_SECRETA_SUPER_LONGA_AQUI_MIN_32_CARACTERES",
    "Issuer": "Resolvia.API",
    "Audience": "Resolvia.Client",
    "ExpiresInMinutes": 60
  },
  "R2Storage": {
    "AccessKey": "SUA_ACCESS_KEY_ID",
    "SecretKey": "SUA_SECRET_ACCESS_KEY",
    "Endpoint": "https://<account_id>.r2.cloudflarestorage.com",
    "PublicUrl": "https://pub-xxxxxxxx.r2.dev",
    "BucketName": "resolvia-attachments"
  }
}
```

> Em produção (Railway), essas configurações são fornecidas via variáveis de ambiente, nunca versionadas no repositório.

---

## Roadmap / possíveis evoluções futuras

- Migrar URLs de anexos para URLs assinadas com expiração (mais seguro que acesso público direto)
- Endpoint de métricas/dashboard (tempo médio de resolução, % dentro do SLA, chamados por categoria)
- Notificações por e-mail em mudanças de status
- Base de conhecimento (FAQ) vinculada às categorias