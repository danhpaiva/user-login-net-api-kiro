# UserApiLogin

REST API para gerenciamento de usuários construída com **.NET 10**, padrão MVC, persistência via **Entity Framework Core + SQLite** e autenticação **JWT Bearer** na rota de exclusão.

---

## Tecnologias

| Pacote                                        | Versão |
| --------------------------------------------- | ------ |
| .NET                                          | 10.0   |
| Microsoft.EntityFrameworkCore.Sqlite          | 10.0.7 |
| Microsoft.EntityFrameworkCore.Design          | 10.0.7 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.7 |
| BCrypt.Net-Next                               | 4.2.0  |
| Swashbuckle.AspNetCore                        | 6.9.0  |

---

## Estrutura do Projeto

```
UserApiLogin/
├── Controllers/
│   ├── AuthController.cs      # Endpoint de login e geração de token JWT
│   └── UsersController.cs     # CRUD de usuários
├── Data/
│   └── AppDbContext.cs        # Contexto do Entity Framework
├── DTOs/
│   ├── LoginDto.cs            # Payload de autenticação
│   ├── RegisterDto.cs         # Payload de criação/atualização
│   └── UserDto.cs             # Resposta pública (sem senha)
├── Migrations/                # Migrations geradas pelo EF Core
├── Models/
│   └── User.cs                # Modelo de domínio
├── Services/
│   └── TokenService.cs        # Geração de tokens JWT
├── appsettings.json
└── Program.cs
```

---

## Modelo de Dados

```csharp
public class User
{
    public int    Id       { get; set; }
    public string Name     { get; set; }
    public string Email    { get; set; }  // único
    public string Password { get; set; }  // hash BCrypt
}
```

A senha nunca é exposta nas respostas da API — todos os endpoints retornam o DTO `UserDto` (Id, Name, Email).

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Ferramenta EF Core CLI (opcional, para gerenciar migrations manualmente):

```bash
dotnet tool install --global dotnet-ef
```

---

## Configuração

As configurações ficam em `appsettings.json`. Em produção, substitua os valores sensíveis por variáveis de ambiente ou um gerenciador de segredos.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=userapi.db"
  },
  "JwtSettings": {
    "SecretKey": "TROQUE_ESTA_CHAVE_EM_PRODUCAO",
    "Issuer": "UserApiLogin",
    "Audience": "UserApiLoginClients",
    "ExpiresInMinutes": "60"
  }
}
```

> **Atenção:** nunca versione a `SecretKey` real em repositórios públicos.

---

## Executando a API

```bash
cd UserApiLogin
dotnet run
```

O banco SQLite (`userapi.db`) é criado e as migrations são aplicadas automaticamente na primeira execução.

URLs padrão:

| Perfil | URL                    |
| ------ | ---------------------- |
| HTTP   | http://localhost:5077  |
| HTTPS  | https://localhost:7253 |

A documentação interativa (Swagger UI) fica disponível em:

```
http://localhost:5077/swagger
```

---

## Endpoints

### Auth

| Método | Rota              | Autenticação | Descrição                                  |
| ------ | ----------------- | ------------ | ------------------------------------------ |
| `POST` | `/api/auth/login` | Não          | Autentica o usuário e retorna um token JWT |

**Body — POST /api/auth/login**
```json
{
  "email": "joao@email.com",
  "password": "senha123"
}
```

**Resposta 200**
```json
{
  "token": "<jwt>",
  "user": {
    "id": 1,
    "name": "João Silva",
    "email": "joao@email.com"
  }
}
```

---

### Users

| Método   | Rota              | Autenticação        | Descrição                     |
| -------- | ----------------- | ------------------- | ----------------------------- |
| `GET`    | `/api/users`      | Não                 | Lista todos os usuários       |
| `GET`    | `/api/users/{id}` | Não                 | Retorna um usuário pelo Id    |
| `POST`   | `/api/users`      | Não                 | Cria um novo usuário          |
| `PUT`    | `/api/users/{id}` | Não                 | Atualiza um usuário existente |
| `DELETE` | `/api/users/{id}` | **JWT obrigatório** | Remove um usuário             |

**Body — POST /api/users e PUT /api/users/{id}**
```json
{
  "name": "João Silva",
  "email": "joao@email.com",
  "password": "senha123"
}
```

**Resposta 201 (criação)**
```json
{
  "id": 1,
  "name": "João Silva",
  "email": "joao@email.com"
}
```

---

## Autenticação JWT

O endpoint `DELETE /api/users/{id}` exige um token JWT válido no header `Authorization`.

### Fluxo

1. Crie um usuário via `POST /api/users`.
2. Autentique-o via `POST /api/auth/login` e copie o `token` retornado.
3. Inclua o token nas requisições protegidas:

```
Authorization: Bearer <token>
```

### Via Swagger UI

1. Acesse `/swagger`.
2. Clique em **Authorize** (ícone de cadeado).
3. Informe `Bearer <token>` e confirme.
4. Execute o endpoint `DELETE`.

---

## Migrations

As migrations são aplicadas automaticamente ao iniciar a aplicação. Para gerenciá-las manualmente:

```bash
# Criar nova migration
dotnet ef migrations add NomeDaMigration

# Aplicar migrations pendentes
dotnet ef database update

# Reverter última migration
dotnet ef migrations remove
```

---

## Códigos de Resposta

| Código             | Significado                  |
| ------------------ | ---------------------------- |
| `200 OK`           | Requisição bem-sucedida      |
| `201 Created`      | Recurso criado com sucesso   |
| `204 No Content`   | Recurso removido com sucesso |
| `400 Bad Request`  | Dados de entrada inválidos   |
| `401 Unauthorized` | Token ausente ou inválido    |
| `404 Not Found`    | Recurso não encontrado       |
| `409 Conflict`     | Email já cadastrado          |

---

## Licença

Distribuído sob a licença [MIT](LICENSE).
