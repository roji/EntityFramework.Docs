---
title: Writing a Database Provider - EF Core
description: Information on writing a new Entity Framework Core provider
author: ajcvickers
ms.date: 03/17/2021
uid: core/providers/writing-a-provider/index
---

# Writing a Database Provider

For information about writing an Entity Framework Core database provider, see [So you want to write an EF Core provider](https://blog.oneunicorn.com/2016/11/11/so-you-want-to-write-an-ef-core-provider/) by [Arthur Vickers](https://github.com/ajcvickers).

> [!NOTE]
> These posts have not been updated since EF Core 1.1 and there have been significant changes since that time.
[Issue 681](https://github.com/dotnet/EntityFramework.Docs/issues/681) is tracking updates to this documentation.

The EF Core codebase is open source and contains several database providers that can be used as a reference. You can find the source code at <https://github.com/dotnet/efcore>. It may also be helpful to look at the code for commonly used third-party providers, such as [Npgsql](https://github.com/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL), [Pomelo MySQL](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql), and [SQL Server Compact](https://github.com/ErikEJ/EntityFramework.SqlServerCompact). In particular, these projects are set up to extend from and run functional tests that we publish on NuGet. This kind of setup is strongly recommended.

## Provider-facing APIs and breaking changes 

EF Core has two kinds of public surface APIs:

1. **User-facing APIs**: These are used by regular EF Core users in their application.
2. **Provider-facing APIs**: These are APIs used only by EF Coer providers.

EF Core takes backwards compatibility very seriously, and breaking changes in user-facing APIs are only done when absolutely necessary. However, we are generally less strict in changes to provider-facing APIs, and do typically introduce breaking changes there in major versions. This is necessary since the interaction between EF Core itself and its providers is very rich, and many new features and performance improvements require such changes; forbidding breaking changes in provider-facing APIs would prevent EF Core from evolving and becoming better.

As a result, it's generally expected that providers need to be updated in order to work with new major versions of EF Core; a provider which works against a specific major version of EF Core will generally not work against another. However, we almost never make breaking changes across patch releases, so providers don't need to react to those.

To help provider maintainers follow relevant changes in EF Core, we use the [`providers-beware`](https://github.com/dotnet/efcore/labels/providers-beware) label on GitHub issues and pull requests. These issues should provide helpful guidance on changes that providers need to react to. In addition, EF Core has an extensive "specification" test suite, where we continuously add tests ensuring that providers behave as expected. This is another way that providers can be made aware of important changes: new (or existing) tests will start to fail. See [The EF Core Specification Tests] for more information.

## Suggested naming of third party providers

We suggest using the following naming for NuGet packages. This is consistent with the names of packages delivered by the EF Core team.

`<Optional project/company name>.EntityFrameworkCore.<Database engine name>`

For example:

* `Microsoft.EntityFrameworkCore.SqlServer`
* `Npgsql.EntityFrameworkCore.PostgreSQL`
* `EntityFrameworkCore.SqlServerCompact40`

