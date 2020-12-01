---
title: Performance Guide - EF Core
description: Performance guide for efficiently using Entity Framework Core
author: roji
ms.date: 12/1/2020
uid: core/miscellaneous/performance/index
---
# Performance Guide

TODO: Fix connection strings in samples
TODO: EF Core runtime perf (overhead) vs. database aspects (e.g. indexes)
TODO: Maybe: sections in the imperative?

## Introduction

Database performance is a vast and complex topic, spanning an entire stack of components: the database, networking, the database driver, and data access layers such as EF Core. While high-level layers and O/RMs such as EF Core considerably simplify application development and improve maintainability, they  can sometimes be opaque, hiding performance-critical internal details such as the SQL being executed. This section attempts to provide an overview of how to achieve good performance with EF Core, and how to avoid common pitfalls which can degrade application performance.

### Identify bottlenecks and measure, measure, measure

As always with performance, it's important not to rush into optimization without data showing a problem; as the great Donald Knuth once said, "Premature optimization is the root of all evil". The [performance diagnosis](xref:core/miscellaneous/performance-diagnosis) section discusses various ways to understand where your application is spending time, and how to pinpoint specific problematic areas. Once a slow query has been identified, solutions can be considered: is your database missing an index? Should you try out other querying patterns?

In particular, beware of basing decisions on general benchmarks and performance claims comparing one data access layer to another. In many cases, these benchmarks are carried out in ideal networking conditions, where latency to the database is almost zero, and with extremely light queries which hardly require any processing (or disk I/O) on the database side. While these are valuable for comparing the runtime overheads of different data access layers, the differences they reveal usually prove to be negligible in a real-world application, where the database performs actual work and latency to the database is a significant perf factor.

TODO: EF Runtime perf vs. database perf.

### Know what's happening under the hood

* Look at the SQL

### Cache outside the database

Finally, the most efficient way to interact with a database is to not interact with it at all. In other words, if database access shows up as a performance bottleneck in your application, it may be worthwhile to cache certain results outside of the database, so as to minimize requests. Although caching adds complexity, it is an especially crucial part of any scalable application: while the application tier can be easy scaled by adding additional servers to handle increased load, scaling the database tier is usually far more complicated.

## Advanced topics

* Compiled queries
* DbContext pooling
* Change tracking - comparers, avoiding snapshotting and deep comparison, proxies, manual tracking. Disable change tracking specifically for a scope (see EF6 guide)

## Additional

* Context lifetime - do not keep the same context (state accumulation)