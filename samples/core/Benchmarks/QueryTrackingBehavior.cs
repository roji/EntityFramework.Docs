// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class QueryTrackingBehavior
    {
        [Params(1)]
        public int NumBlogs { get; set; }

        [Params(5000)]
        public int NumPostsPerBlog { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine("Setting up database...");
            using var context = new BloggingContext();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            context.SeedData(NumBlogs, NumPostsPerBlog);
            Console.WriteLine("Setup complete.");
        }

        [Benchmark]
        public List<Post> Tracking()
        {
            using var context = new BloggingContext();

            return context.Posts.Include(p => p.Blog).ToList();
        }

        [Benchmark]
        public List<Post> NoTracking()
        {
            using var context = new BloggingContext();

            return context.Posts.AsNoTracking().Include(p => p.Blog).ToList();
        }

        [Benchmark]
        public List<Post> NoTrackingWithIdentityResolution()
        {
            using var context = new BloggingContext();

            return context.Posts.AsNoTrackingWithIdentityResolution().Include(p => p.Blog).ToList();
        }

        public class BloggingContext : DbContext
        {
            public DbSet<Blog> Blogs { get; set; }
            public DbSet<Post> Posts { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer(
                    @"Server=localhost;Database=test;User=SA;Password=Abcd5678;Connect Timeout=60;ConnectRetryCount=0");
                // @"Server=(localdb)\mssqllocaldb;Database=Blogging;Integrated Security=True");
            }

            public void SeedData(int numBlogs, int numPostsPerBlog)
            {
                using var context = new BloggingContext();
                context.AddRange(
                    Enumerable.Range(0, numBlogs).Select(_ => new Blog
                    {
                        Posts = Enumerable.Range(0, numPostsPerBlog).Select(_ => new Post()).ToList()
                    }));
                context.SaveChanges();
            }
        }

        public class Blog
        {
            public int BlogId { get; set; }
            public string Url { get; set; }
            public int Rating { get; set; }
            public List<Post> Posts { get; set; }
        }

        public class Post
        {
            public int PostId { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }

            public int BlogId { get; set; }
            public Blog Blog { get; set; }
        }
    }
}
