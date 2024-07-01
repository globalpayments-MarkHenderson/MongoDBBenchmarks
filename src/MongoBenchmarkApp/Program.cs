// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MongoDB.Driver;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using MongoDB.Bson;

namespace MongoDbBenchmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<MongoDbBenchmarks>();
        }
    }
   
    public class MongoDbBenchmarks
    {
        private IMongoCollection<MyDocument> _collection;
        private MyDbContext _dbContext;

        [GlobalSetup]
        public void Setup()
        {          
            var client = new MongoClient("mongodb://root:strongpassword@localhost:27017");
            var database = client.GetDatabase("benchmarkDb");
            _collection = database.GetCollection<MyDocument>("documents_mongo");

            // Assuming you have a DbContext setup for Entity Framework
            _dbContext = new MyDbContext();

            // Setup code here, like ensuring the database is in a known state
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Using MongoDB driver to clean up
            _collection.DeleteMany(Builders<MyDocument>.Filter.Empty); // Deletes all documents

            // Using Entity Framework to clean up
            var allDocuments = _dbContext.Documents.ToList();
            _dbContext.Documents.RemoveRange(allDocuments);
            _dbContext.SaveChanges();
        }


        [Benchmark]
        public void InsertDocumentMongoDriver()
        {
            _collection.InsertOne(new MyDocument { Id = MongoDB.Bson.ObjectId.GenerateNewId(), Name = "Test", Value = "SomeValue" });
        }

        [Benchmark]
        public void InsertDocumentEntityFramework()
        {
            _dbContext.Documents.Add(new MyDocument { Id = MongoDB.Bson.ObjectId.GenerateNewId(),  Name = "Test", Value = "SomeValue" });
            _dbContext.SaveChanges();
        }

        [Benchmark]
        public void QueryDocumentMongoDriver()
        {
            var filter = Builders<MyDocument>.Filter.Eq("Name", "Test");
            var result = _collection.Find(filter).FirstOrDefault();
        }

        [Benchmark]
        public void QueryDocumentEntityFramework()
        {
            var result = _dbContext.Documents.FirstOrDefault(d => d.Name == "Test");
        }

        [Benchmark]
        public void BatchInsertMongoDriver()
        {
            var documents = new List<MyDocument>();
            for (int i = 0; i < 1000; i++) // A batch of 1000 documents
            {
                documents.Add(new MyDocument { Id = MongoDB.Bson.ObjectId.GenerateNewId(), Name = $"Test{i}", Value = $"SomeValue{i}" });
            }
            _collection.InsertMany(documents);
        }

        [Benchmark]
        public void BatchInsertEntityFramework()
        {
            var documents = new List<MyDocument>();
            for (int i = 0; i < 1000; i++) // A batch of 1000 documents
            {
                documents.Add(new MyDocument { Id = MongoDB.Bson.ObjectId.GenerateNewId(), Name = $"Test{i}", Value = $"SomeValue{i}" });
            }
            _dbContext.Documents.AddRange(documents);
            _dbContext.SaveChanges();
        }

        [Benchmark]
        public void UpdateDocumentMongoDriver()
        {
            var filter = Builders<MyDocument>.Filter.Eq(doc => doc.Name, "Test");
            var update = Builders<MyDocument>.Update.Set(doc => doc.Value, "UpdatedValue");
            _collection.UpdateOne(filter, update);
        }

        [Benchmark]
        public void UpdateDocumentEntityFramework()
        {
            var document = _dbContext.Documents.FirstOrDefault(d => d.Name == "Test");
            if (document != null)
            {
                document.Value = "UpdatedValue";
                _dbContext.SaveChanges();
            }
        }

        [Benchmark]
        public void BatchUpdateMongoDriver()
        {            
            var idsToUpdate = _collection.Find(Builders<MyDocument>.Filter.Empty)
                                         .Limit(100)
                                         .ToList()
                                         .Select(doc => doc.Id);

            
            var filter = Builders<MyDocument>.Filter.In(doc => doc.Id, idsToUpdate);
            var update = Builders<MyDocument>.Update.Set(doc => doc.Value, "BatchUpdatedValue");
            _collection.UpdateMany(filter, update);
        }




        [Benchmark]
        public void BatchUpdateEntityFramework()
        {
            var documents = _dbContext.Documents.OrderBy(d => d.Id).Take(100).ToList(); // Ensure there are 100 records
            foreach (var document in documents)
            {
                document.Value = "BatchUpdatedValue";
            }
            _dbContext.SaveChanges();
        }



        [Benchmark]
        public void DeleteDocumentMongoDriver()
        {
            var filter = Builders<MyDocument>.Filter.Eq("Name", "Test");
            _collection.DeleteOne(filter);
        }

        [Benchmark]
        public void DeleteDocumentEntityFramework()
        {
            var document = _dbContext.Documents.FirstOrDefault(d => d.Name == "Test");
            if (document != null)
            {
                _dbContext.Documents.Remove(document);
                _dbContext.SaveChanges();
            }
        }

        [Benchmark]
        public void BatchDeleteMongoDriver()
        {
            var filter = Builders<MyDocument>.Filter.Empty; 
            _collection.DeleteMany(filter);
        }

        [Benchmark]
        public void BatchDeleteEntityFramework()
        {
            var documents = _dbContext.Documents.ToList();
            _dbContext.Documents.RemoveRange(documents);
            _dbContext.SaveChanges();
        }


    }

    public class MyDocument
    {
        public MongoDB.Bson.ObjectId Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    // Define your DbContext and DbSet if using Entity Framework
    public class MyDbContext : DbContext
    {
        public DbSet<MyDocument> Documents { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMongoDB("mongodb://root:strongpassword@localhost:27017","benchmarkDb");
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyDocument>().HasKey(d => d.Id);
            modelBuilder.Entity<MyDocument>().ToCollection("documents_ef");            
            base.OnModelCreating(modelBuilder);
        }
    }

}
