---
title: Writing Efficient Queries - EF Core
description: Performance guide for efficiently using Entity Framework Core
author: roji
ms.date: 12/1/2020
uid: core/miscellaneous/writing-efficient-queries
---
# Writing Efficient Queries

## Use indexes properly

The main deciding factor in whether a query will run fast or not is whether it will properly utilize indexes where appropriate: databases are typically used to hold large amounts of data, and queries which traverse entire tables are generally sources of serious performance issues. Indexing issues aren't easy to spot, because it isn't immediately obvious whether a given query will use an index or not. For example:

```csharp
_ = ctx.Blogs.Where(b => b.Name.StartsWith("A")).ToList(); // Uses an index defined on Name on SQL Server
_ = ctx.Blogs.Where(b => b.Name.EndsWith("B")).ToList(); // Does not use the index
```

The main way the spot indexing issues is to first pinpoint a slow query, and then examine its query plan via your database's favorite tool; see the [performance diagnosis](xref:core/miscellaneous/performance-diagnosis) for more information on how to do that. The query plan should display whether the query traverses the entire table, or uses an index (and which one).

As a general rule, there isn't any special EF knowledge to using indexes or diagnosing performance issues related to them; general database knowledge related to indexes is just as relevant to EF applications as to applications not using EF. The following lists some general guidelines to keep in mind when using indexes:

* While indexes speed up queries, they also slow slows down updates since they need to be kept up-to-date. Avoid defining indexes which aren't needed, and consider using [index filters](core/modeling/indexes#index-filter) to limit the index to a subset of the rows, thereby reducing this overhead.
* Composite indexes can speed up queries which filter on multiple columns. But they can also speed up queries which don't filter on all the index's columns. For example, an index on columns A and B speed up queries filtering by A and B, as well as queries filtering only by A, but it does not speed up queries filtering over only by B.
* If a query filters by an expression over a column (e.g. `price / 2`), a simple index cannot be used. However, you can define a [stored persisted column](xref:core/modeling/generated-properties#computed-columns) for your expression, and create an index over that. Some database also support expression indexes, which can be directly used to speed up queries filtering by any expression.
* Different databases allow indexes to be configured in various ways, and in many cases EF Core providers exposed these configurations via an easy Fluent API. For example, the SQL Server provider allows you to configure whether an index is [clustered](xref:core/providers/sql-server/indexes#clustering), or its [fill factor](xref:core/providers/sql-server/indexes#fill-factor), consult your provider's documentation.

## Select only properties you need

EF Core makes it very easy to query out entity instances, and then use those instances in code. However, querying entity instances can frequently pull back more data than necessary from your database. Consider the following:

```csharp
foreach (var blog in ctx.Blogs)
{
    Console.WriteLine("Blog: " + blog.Url);
}
```

Although this code only actually needs each Blog's `Url` property, the entire Blog entity is fetched, and unneeded columns are transferred from the database:

```sql
SELECT [b].[BlogId], [b].[CreationDate], [b].[Name], [b].[Rating], [b].[Url]
FROM [Blogs] AS [b]
```

This can be optimized by using `Select` to tell EF which columns to project out:

```csharp
foreach (var blogName in ctx.Blogs.Select(b => b.Url))
{
    Console.WriteLine("Blog: " + blogName);
}
```

The resulting SQL pulls back only the needed columns:

```csharp
SELECT [b].[Url]
FROM [Blogs] AS [b]
```

If you need to project out more than one column, project out to a C# anonymous type with the properties you want.

Note that this technique is very useful for read-only queries, but things get more complicated if you need to *update* the fetched blogs - EF's change tracking only works with entity instances. It's possible perform updates by attaching a modified Blog instance and telling EF which properties have changed, but that is a more advanced technique that may not be worth it.

## Limit the resultset size

By default, a query returns all rows that matches its filters:

```csharp
var blogs = ctx.Blogs
    .Where(b => b.Name.StartsWith("A"))
    .ToList();
```

Since the number of rows returned depends on actual data in your database, it's impossible to know how much data will be loaded from the database; how much memory will be taken up by the results; and how much additional load will be generated when processing these results (e.g. by sending them to a user browser over the network). In addition, it's common for test databases to contain little data, so that everything works well while testing, but performance problems suddenly appear when the query starts running on real-world data and many rows are returned.

As a result, it's usually worth giving thought to limiting the number of results:

```csharp
var blogs = ctx.Blogs
    .Where(b => b.Name.StartsWith("A"))
    .Take(25)
    .ToList();
```

At a minimum, your UI could show a message indicating that more rows may exist in the database (and allow retrieving them in some other manner). A full-blown solution would implement *paging*, where your UI only shows a certain number of rows at a time, and allow users to advance to the next page as needed; this typically combines the <xref:System.Linq.Enumerable.Take%2A> and <xref:System.Linq.Enumerable.Skip%2A> operators to select a specific range in the resultset each time.

## Using raw SQL


## Query caching and parameterization

When EF receives a LINQ query tree for execution, it must first "compile" that tree into a SQL query. Because this is a heavy process, EF caches queries by the query tree *shape*: queries with the same structure reuse internally-cached compilation outputs, and can skip repeated compilation. The different queries may still reference different *values*, but as long as these values are properly parameterized, the structure is the same and caching will function properly.

Consider the following two queries:

```csharp
var blog1 = ctx.Blogs.FirstOrDefault(b => b.Name == "blog1");
var blog2 = ctx.Blogs.FirstOrDefault(b => b.Name == "blog2");
```

Since the expression trees contains different constants, the expression tree differs and each of these queries will be compiled separately by EF Core. In addition, each query produces a slightly different SQL command:

```sql
SELECT TOP(1) [b].[Id], [b].[Name]
FROM [Blogs] AS [b]
WHERE [b].[Name] = N'blog1'

SELECT TOP(1) [b].[Id], [b].[Name]
FROM [Blogs] AS [b]
WHERE [b].[Name] = N'blog2'
```

Because the SQL differs, your database server will likely also need to produce a query plan for both queries, rather than reusing the same plan.

A small modification to your queries can change things considerably:

```csharp
var blogName = "blog1";
var blog1 = ctx.Blogs.FirstOrDefault(b => b.Name == blogName);
blogName = "blog2";
var blog2 = ctx.Blogs.FirstOrDefault(b => b.Name == blogName);
```

Since the blog name is now *parameterized*, both queries have the same tree shape, and EF only needs to be compiled once. The SQL produced is also parameterized, allowing the database to reuse the same query plan:

```sql
SELECT TOP(1) [b].[Id], [b].[Name]
FROM [Blogs] AS [b]
WHERE [b].[Name] = @__blogName_0
```

Note that there is no need to parameterize each and every query: it's perfectly fine to have some queries with constants, and indeed, databases (and EF) can sometimes perform certain optimization around constants which aren't possible when the query is parameterized. See the section on [dynamically-constructed queries](#dynamically-constructed-queries) for an example where proper parameterization is crucial.

> [!NOTE]
> EF Core's [event counters](xref:core/logging-events-diagnostics/event-counters) report the Query Cache Hit Rate. In a normal application, this counter reaches 100% soon after program startup, once most queries have executed at least once. If this counter remains stable below 100%, that is an indication that your application may be doing something which defeats the query cache - it's a good idea to investigate that.

> [!NOTE]
> How the database manages caches query plans is database-dependent. For example, SQL Server implicitly maintains an LRU query plan cache, whereas PostgreSQL does not (but prepared statements can produce a very similar end effect). Consult your database documentation for more details.

## Dynamically-constructed queries

In some situations, it is necessary to dynamically construct LINQ queries rather than specifying them outright in source code. This can happen, for example, in a website which receives arbitrary query details from a client, with open-ended query operators (sorting, filtering, paging...). In principle, if done correctly, dynamically-constructed queries can be just as efficient as regular ones (although it's not possible to use the [compiled query]() optimization with dynamic queries). In practice, however, they are frequently the source of performance issues, since it's easy to accidentally produce expression trees with shapes that differ every time.

The following example uses two techniques to dynamically construct a query; we add a Where operator to the query only if the given parameter is not null. Note that this isn't a good use case for dynamically constructing a query - but we're using it for simplicity:

### [With constant](#tab/with-constant)

[!code-csharp[Main](../../../samples/core/Benchmarks/DynamicallyConstructedQueries.cs.cs?name=WithConstant&highlight=14-24)]

### [With parameter](#tab/with-parameter)

[!code-csharp[Main](../../../samples/core/Benchmarks/DynamicallyConstructedQueries.cs.cs?name=WithParameter&highlight=14)]

***

Benchmarking these two techniques gives the following results:

|        Method |       Mean |    Error |    StdDev |   Gen 0 |  Gen 1 | Gen 2 | Allocated |
|-------------- |-----------:|---------:|----------:|--------:|-------:|------:|----------:|
|  WithConstant | 1,096.7 us | 12.54 us |  11.12 us | 13.6719 | 1.9531 |     - |  83.91 KB |
| WithParameter |   570.8 us | 42.43 us | 124.43 us |  5.8594 |      - |     - |  37.16 KB |

Even if the sub-millisecond difference seems small, keep in mind that the constant version continuously pollutes the cache and causes other queries to be re-compiled, slowing them down as well.

> [!NOTE]
> Avoid constructing queries with the expression tree API unless you really need to. Aside from the API's complexity, it's very easy to inadvertently cause significant performance issues when using them.

## TODO

## Raw SQL where EF isn't good enough (the different options: FromSql, function, DbSet mapping to function)
## Client evaluation, bring as little data as possible
## Call out Contains + array parameter specifically?
