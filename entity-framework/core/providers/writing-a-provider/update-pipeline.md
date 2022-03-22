---
title: Writing a Database Provider - Update Pipeline - EF Core
description: The EF Core update pipeline
author: roji
ms.date: 03/17/2021
uid: core/providers/writing-a-provider/update-pipeline
---

# The EF Core Update Pipeline

The update pipeline is responsible for implementing the machinery behind <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges%2A>, accepting all the changes accumulated by the change tracker (additions, modifications, deletions), and applying them to the database.

> [!NOTE]
> The update pipeline does not manage bulk updates, even though those are updates well. Bulk updates do not interact with the change tracker, and allow expressing arbitrary SQL queries via LINQ, and so are implemented via the query pipeline.

## Update pipeline starting point

The entry point into the update pipeline is the provider's implementation of the <xref:Microsoft.EntityFrameworkCore.Storage.IDatabase> interface, which contains a <xref:Microsoft.EntityFrameworkCore.Storage.IDatabase.SaveChanges> and a <xref:Microsoft.EntityFrameworkCore.Storage.IDatabase.SaveChangesAsync> method. Let's examine these basic APIs ([source code](https://github.com/dotnet/efcore/blob/main/src/EFCore/Storage/IDatabase.cs)):

```c#
public abstract Task<int> SaveChangesAsync(
    IList<IUpdateEntry> entries,
    CancellationToken cancellationToken = default);
```

The input to the method - and to the update pipeline as a whole - is a list of <xref:Microsoft.EntityFrameworkCore.Update.IUpdateEntry> instances, which represents changes to be applied. This list is produced by the change tracker, which itself isn't affected by providers or customized by them.

As usual, if you're implementing a relational database, a <xref:Microsoft.EntityFrameworkCore.Update.RelationalDatabase> implementation is already provided for you, which does most of the heavy lifting for applying updates via SQL; the rest of this article will describe the relational implementation and the provider extension points in it. However, if you're writing a non-relational provider, then it's up to you to implement `IDatabase` yourself, and save the changes in the way supported by your database. [The Cosmos implementation](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Storage/Internal/CosmosDatabaseWrapper.cs) may be useful for understanding what needs to be done.

## Overview of the relational implementation

Let's take a look the relational implementation of <xref:Microsoft.EntityFrameworkCore.Storage.IDatabase.SaveChangesAsync> (note that this is already provided by EF Core and does not need to be implemented in your provider):

```c#
public override Task<int> SaveChangesAsync(
    IList<IUpdateEntry> entries,
    CancellationToken cancellationToken = default)
    => RelationalDependencies.BatchExecutor.ExecuteAsync(
        RelationalDependencies.BatchPreparer.BatchCommands(
            entries,
            Dependencies.UpdateAdapterFactory.Create()),
        RelationalDependencies.Connection,
        cancellationToken);
```

Two components are at work here: <xref:Microsoft.EntityFrameworkCore.Update.ICommandBatchPreparer> is responsible for transforming the flat list of `IUpdateEntry` instances into a set of *batches*, and <xref:Microsoft.EntityFrameworkCore.Update.IBatchExecutor> is responsible for executing those batches against the database. Both these components are still part of EF's relational implementation, and providers are not expected to customize them.

This brings up an important aspect the relational update pipeline: it is designed around the concept of batching, enabling it to apply multiple updates in a single database roundtrip (or in only a few of them). This is much more efficient than performing a roundtrip for each and every update. Typically this would mean combining multiple SQL DML statements in a single ADO.NET `DbCommand`, e.g. `UPDATE ... ; DELETE ... ; INSERT ...`.

## IModificationCommand and IColumnModification