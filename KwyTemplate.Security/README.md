# KwyTemplate.Security

`KwyTemplate.Security` 是模板项目的单机安全模块，负责本地账号登录、当前用户状态、权限判断、账号初始化和可选授权检查。

当前模板不设计 `Guest / 未登录` 等级。程序启动后，在未登录或高级用户会话超时后，系统切换到 `Operator` 操作员语义，避免业务层到处判断“未登录”。

## 分层关系

权限码放在 `KwyTemplate.Contracts.Security.PermissionCodes` 中：

```text
KwyTemplate.App      -> KwyTemplate.Contracts
KwyTemplate.Security -> KwyTemplate.Contracts
```

App 只消费权限码，Security 负责解释权限规则。后续把权限来源从本地 SQLite 换成密码狗、MES、LDAP 或远程权限服务时，业务模块不需要修改。

## 角色等级

| 等级 | 说明 | 继承关系 |
| --- | --- | --- |
| `Operator` | 操作员，适合日常生产操作 | 只拥有操作员权限 |
| `Engineer` | 工程师，适合参数、配方、流程调试 | 拥有工程师 + 操作员权限 |
| `Admin` | 管理员，适合用户管理、系统设置和高级维护 | 拥有管理员 + 工程师 + 操作员权限 |

核心判断是等级比较：

```csharp
currentUser.Level >= SecurityUserLevel.Engineer
```

因此 `Engineer` 天然拥有 `Operator` 的所有功能，`Admin` 天然拥有 `Engineer` 的所有功能。

## 会话超时

`SecuritySessionOptions` 用于配置高级用户登录时长。`Operator` 不启动超时计时；`Engineer` 和 `Admin` 使用同一个高级用户会话时长，到期后会自动切换为操作员用户，系统回到 `Operator` 语义。

模板中可以在 `KwyTemplate.Shell.App.RegisterTypes` 配置：

```csharp
services.AddSingleton(new SecuritySessionOptions
{
    ElevatedUserSessionDuration = TimeSpan.FromMinutes(30)
});
```

如果要关闭自动回到操作员，可以把时长设为 `TimeSpan.Zero`：

```csharp
services.AddSingleton(new SecuritySessionOptions
{
    ElevatedUserSessionDuration = TimeSpan.Zero
});
```

## PermissionCommand

Security 模块会注册 `IPermissionService`，`Kwy.MVVM` 的 `PermissionCommand` 会自动使用该服务。

推荐业务模块使用 Contracts 中的权限码常量：

```csharp
using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Security;

SaveRecipeCommand = new DelegateCommand(SaveRecipe)
    .WithPermission(PermissionCodes.Engineer);

UserManageCommand = new DelegateCommand(OpenUserManager)
    .WithPermission(PermissionCodes.Admin);
```

导航项同样使用权限码常量：

```csharp
new NavigationItemModel
{
    ViewName = ViewNames.SetView,
    DisplayText = "设置",
    Icon = IconNames.IconSet,
    PermissionCode = PermissionCodes.Engineer
};
```

未登录时按 `Operator` 判断，所以操作员权限默认可用，工程师和管理员权限需要登录后才能使用。

## 默认账号

首次运行时，如果 `Users` 表为空，会自动创建三个默认账号：

| 用户名 | 默认密码 | 等级 | 说明 |
| --- | --- | --- | --- |
| `operator` | `operator123` | `Operator` | 操作员 |
| `engineer` | `engineer123` | `Engineer` | 工程师 |
| `admin` | `admin123` | `Admin` | 管理员 |

正式项目中建议在交付前修改默认密码，或在首次启动向导中强制修改。

## 核心服务

| 服务 | 职责 |
| --- | --- |
| `ICurrentUserService` | 保存当前用户状态，处理高级用户会话超时自动切换操作员，并在用户切换时通知 UI 与权限系统。 |
| `IPermissionService` | 解释权限码，判断当前用户是否拥有权限，并提供无权限提示文案。 |
| `ILoginService` | 处理本地账号登录。 |
| `IAuthenticationDialogService` | 打开登录对话框，并返回登录后的当前用户。 |
| `ISecurityKeyChecker` | 可选授权入口，例如密码狗或商业库授权。 |
| `LocalUserStore` | 基于 EF Core SQLite 的本地账号存储。 |

`ICurrentUserService` 表达“当前是谁”，`IPermissionService` 表达“当前用户能不能做某件事”。这两个职责不要合并，否则 UI、命令和权限规则会互相缠在一起。

## 数据模型

`LocalUser` 使用以下核心字段：

| 字段 | 说明 |
| --- | --- |
| `UserName` | 登录名，唯一。 |
| `DisplayName` | 显示名。 |
| `PasswordHash` / `PasswordSalt` | 密码哈希与盐。 |
| `Level` | 用户等级，对应 `SecurityUserLevel`。 |
| `IsEnabled` | 是否允许登录。 |
| `CreatedAt` | 创建时间。 |

旧的 `IsAdmin` 权限路径已经移除，后续统一使用 `Level` 与权限码判断。
