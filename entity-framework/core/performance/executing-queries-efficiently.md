---
title: Executing Queries Efficiently - EF Core
description: Performance guide for efficiently using Entity Framework Core
author: roji
ms.date: 12/1/2020
uid: core/miscellaneous/executing-queries-efficiently
---
# Executing Queries Efficiently

Even if your LINQ query is optimized and properly uses index, the way you execute it can have a significant impact on performance. In many cases, the optimal way to execute a query depends on the amount of data it will be bringing back (and on other factors), so there isn't one optimal way to run all queries. This section details the various ways to execute queries in EF, and provides tips for optimal querying performance.

## Buffering and streaming queries

Buffering refers to loading all your query results into memory, whereas streaming means that that EF hands the application a single result each time, never containing the entire resultset in memory. In principle, the memory requirements of a streaming query are generally fixed - they are the same whether the query returns 1 row or 1000; a buffering query, on the other hand, requires more memory the more rows are returned. For queries that result large resultsets, this can be query.

Whether a query buffers or streams depends on how it is evaluated:

```csharp
// ToList and ToArray cause the entire resultset to be buffered:
var blogsList = context.Blogs.Where(b => b.Name.StartsWith("A")).ToList();
var blogsArray = context.Blogs.Where(b => b.Name.StartsWith("A")).ToArray();

// Foreach streams, processing one row at a time:
foreach (var blog in context.Blogs.Where(b => b.Name.StartsWith("A")))
{
    // ...
}

// AsEnumerable also streams, allowing you to execute LINQ operators on the client-side:
var groupedBlogs = context.Blogs
    .Where(b => b.Name.StartsWith("A"))
    .AsEnumerable()
    .Where(b => SomeDotNetMethod(b));
```

If your queries return just a few results, then you probably don't have to worry about this. However, if your query might return large numbers of rows, it's worth giving careful thought

> [!NOTE]
> When possible, it is always recommended to limit the number of rows your query returns with <xref:System.Linq.Enumerable.Take%2A>, and to use paging.

### Internal buffering by EF

In certain situations, EF will itself buffer the resultset internally, regardless of how you evaluate your query. The two cases where this happens are:

* When a retrying execution strategy is in place.
* When [split query](xref:core/querying/single-split-queries) is used, the resultsets of all but the last query are buffered - unless MARS is enabled on SQL Server. This is because it is normally not possible to have multiple query resultsets active at the same time.

Note that this internal buffering occurs in addition to any buffering you cause via LINQ operators. For example, if you use <xref:System.Linq.Enumerable.ToList%2A> on a query and a retrying execution strategy is in place, the resultset is loaded into memory *twice*: once internally by EF, and once by <xref:System.Linq.Enumerable.ToList%2A>.

## Tracking, no-tracking and identity resolution

It's recommended to read [the dedicated page on tracking and no-tracking](xref:core/querying/tracking) before continuing with this section.

EF tracks entity instances by default, so that changes on them are detected and persisted when <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges%2A> is called. Another effect of tracking queries is that EF will detect if an instance has already been loaded for a database row, and will automatically return that tracked instance rather than returning a new one; this is called *identity resolution*. From a performance perspective, change tracking means the following:

* EF internally maintains a dictionary of tracked instances internally. When new data is loaded, EF checks the dictionary to see if an instance is already tracked for that entity's key (identity resolution). These dictionary lookups take up some time when loading the query's results.
* Before handing a loaded instance to the application, EF *snapshots* that instance and keeps the snapshot internally. When <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges%2A> is called, the application's instance will be compared with the snapshot to discover the changes to be persisted. The snapshot takes up more memory, and the snapshotting process also takes time; it's possible specify different, possibly more efficient snapshotting behavior via [value comparers](), or to use [change-tracking proxies]() to bypass the snapshotting process altogether.

It's possible to avoid the above overheads by using [no-tracking queries](xref:core/querying/tracking#no-tracking-queries), and this is especially appropriate in read-only scenarios, when no changes will be saved back to the database. However, since no-tracking queries do not perform identity resolution, a database row which is referenced by multiple other loaded rows will be materialized as as different instances, taking up more memory.

To illustrate, assume we are loading a large number of Posts from the database, as well as the Blog referenced by each Post. If 100 Posts happen to reference the same Blog, a tracking query detects this via identity resolution, and all Post instances will refer the same de-duplicated Blog instance. A no-tracking query, in contrast, duplicates the same Blog 100 times, resulting in increased memory requirements and garbage.

BENCHMARK?

EF Core 5.0 introduced a 3rd query mode, called [no tracking with identity resolution](https://docs.microsoft.com/en-us/ef/core/querying/tracking#identity-resolution), which can provide the best of both worlds. This mode does perform identity resolution - performing dictionary lookups as data is loaded and de-duplicates to a single instance - but does not snapshot or track the instance beyond the execution time of the query. Whether the dictionary lookup overhead is worth the memory savings brought about by de-duplication depends on the specific data returned by the query.

Finally, it is possible to perform updates without the overhead of change tracking, by utilizing a no-tracking query and then attaching the returned instance to the context, specifying which changes are to be made. This transfers the burden of change tracking from EF to the user, and should only be attempted if the change tracking overhead has been shown to be unacceptable via profiling or benchmarking.

```csharp
EXAMPLE
```

BENCHMARK?

## Database roundtrips

TODO: move this blurb to the intro and make the sections level-2?

A major factor in database performance is the overhead of network roundtrips, since the time taken for a query to execute in the database is often dwarfed by the time packets travel back and forth between your application and your database. Roundtrip overhead heavily depends on your environment; the further away your database server is, the high the latency and the costlier each roundtrip. With the advent of the cloud, applications increasingly find find themselves further away from the database, and performance is degraded in applications performing many roundtrips. Therefore, it's important to understand exactly when your application contacts the database, how many roundtrips it performs, and whether that number can be minimized them.

## Querying related entities

When dealing with related entities, 

When writing queries, it's important to know whether relevant related entities will definitely be needed

> [!NOTE]
> The current implementation of [split queries](xref:core/querying/single-split-queries) executes a roundtrip for each query. We plan to improve this in the future, and execute all queries in a single roundtrip.

## Lazy loading

[Lazy loading](xref:core/querying/related-data/lazy) often seems like a very useful way to write database logic. EF Core automatically loads related entities from the database as they are accessed by your code. This avoids loading related entities that aren't needed (like [explicit loading](xref:core/querying/related-data/explicit)), and seemingly frees the programmer from having to explicitly deal with this task. However, lazy loading is particularly prone for producing unneeded extra roundtrips which can slow the application.

Consider the following:

```csharp
foreach (var blog in ctx.Blogs.ToList())
{
    foreach (var post in blog.Posts)
    {
        Console.WriteLine($"Blog {blog.Url}, Post: {post.Title}");
    }
}
```

This seemingly innocent code iterates through all the blogs and their posts, printing them out. Turning on EF Core's [statement logging](xref:core/logging-events-diagnostics/index) reveals the following:

```console
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT [b].[BlogId], [b].[Rating], [b].[Url]
      FROM [Blogs] AS [b]
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (5ms) [Parameters=[@__p_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Title]
      FROM [Post] AS [p]
      WHERE [p].[BlogId] = @__p_0
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[@__p_0='2'], CommandType='Text', CommandTimeout='30']
      SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Title]
      FROM [Post] AS [p]
      WHERE [p].[BlogId] = @__p_0
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[@__p_0='3'], CommandType='Text', CommandTimeout='30']
      SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Title]
      FROM [Post] AS [p]
      WHERE [p].[BlogId] = @__p_0

... and so on
```

What's going on here, why are all these queries being sent for the simple loops above? With lazy loading, a Blog's Posts are only loaded when its Posts property is accessed; as a result, each iteration in the inner foreach triggers an additional database query, in its own roundtrip. Therefore, after the initial query loading all the blogs, we then have another query *per blog*, loading all its posts; this is sometimes called the *N+1* problem, and it can cause very significant performance issues.

Since we know we're going to access all the blogs on all posts, it makes sense to use eager loading here instead. We can use the [Include](xref:core/querying/related-data/eager#eager-loading) operator, but it's worth noting that we only need the Blogs' URLs (and we should only [load what's needed](xref:core/miscellaneous/writing-efficient-queries#select-only-properties-you-need)). So we'll use a projection instead:

```csharp
foreach (var blog in ctx.Blogs.Select(b => new { b.Url, b.Posts }).ToList())
{
    foreach (var post in blog.Posts)
    {
        Console.WriteLine($"Blog {blog.Url}, Post: {post.Title}");
    }
}
```

This will make EF Core fetch all the Blogs - along with their Posts - in a single query. In some cases, it may also be useful to avoid cartesian explosion effects by using [split queries]().

> [!WARNING]
> Because lazy loading makes it extremely easy to inadvertently trigger the N+1 problem, it is recommended to avoid it. Eager or explicit loading make it very clear in the source code when a database roundtrip occurs.

## Asynchronous programming

As a general rule, in order for your application to be scalable, it's important to always use asynchronous APIs rather than synchronous one (e.g. <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync> rather than <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges>). Synchronous APIs block the thread for the duration of database I/O, increasing the need for threads and the number of thread context switches that must occur.

For more information, see the page on [async programming](core/miscellaneous/async).

> [!WARNING]
> Avoid mixing synchronous and asynchronous code in the same application - it's very to inadvertently trigger subtle thread-pool starvation issues.

## TODO

* Related entities
    * Cartesian explosion - split vs. single
    * Eager vs. lazy loading (see EF6 guide) - are you sure you need all the data or not. Explicit loading.

