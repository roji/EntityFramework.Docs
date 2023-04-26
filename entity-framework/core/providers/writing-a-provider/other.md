---
title: Writing a Database Provider - Other - EF Core
description: Other topics for writing an EF Core database provider
author: roji
ms.date: 04/26/2023
uid: core/providers/writing-a-provider/other
---
# Writing a provider - other topics

## Primitive collections

EF Core 8.0 is introduced *primitive collection* support, which refers to storing and querying collections of non-entity types.

Note that primitive collections don't have to be stored as JSON arrays; the specific representation is provider-specific. For example, PostgreSQL supports native arrays in the database, and so primitive collections are represented using that rather than strings containing JSON arrays.
