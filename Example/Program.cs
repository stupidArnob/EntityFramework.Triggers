﻿using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFramework.Triggers;

namespace Example {
	public class Program {
		public abstract class Trackable {
			public virtual DateTime Inserted { get; private set; } // protected set; if EF Core
			public virtual DateTime Updated { get; private set; } // protected set; if EF Core

			static Trackable() {
				Triggers<Trackable>.Inserting += entry => entry.Entity.Inserted = entry.Entity.Updated = DateTime.UtcNow;
				Triggers<Trackable>.Updating += entry => entry.Entity.Updated = DateTime.UtcNow;
			}
		}

		public abstract class SoftDeletable : Trackable {
			public virtual DateTime? Deleted { get; private set; } // protected set; if EF Core

			static SoftDeletable() {
				Triggers<SoftDeletable>.Deleting += entry => {
					entry.Entity.Deleted = DateTime.UtcNow;
					entry.Cancel = true; // Cancels the deletion, but will persist changes with the same effects as EntityState.Modified
				};
			}
		}

		public class Person : SoftDeletable {
			public virtual Int64 Id { get; private set; }
			public virtual String FirstName { get; set; }
			public virtual String LastName { get; set; }

			public override String ToString() => $"{Inserted}\t{LastName}, {FirstName}";
		}
		public class LogEntry {
			public virtual Int64 Id { get; private set; }
			public virtual String Message { get; set; }
		}

		public class Context : DbContext {// WithTriggers {
			public virtual DbSet<Person> People { get; protected set; }
			public virtual DbSet<LogEntry> Log { get; protected set; }

			public override Int32 SaveChanges() => this.SaveChangesWithTriggers(base.SaveChanges);
			public override Task<Int32> SaveChangesAsync(CancellationToken ct) => this.SaveChangesWithTriggersAsync(base.SaveChangesAsync, ct);
		}

		internal sealed class Configuration : DbMigrationsConfiguration<Context> {
			public Configuration() {
				AutomaticMigrationsEnabled = true;
			}
		}

		static Program() {
			Triggers<Object>.Inserting += e => Console.WriteLine("Inserting " + e.Entity.GetType().Name);
			//Triggers<LogEntry>.Inserting += e => Console.WriteLine("Inserting LogEntry");
			Triggers<Person, Context>.Inserting += e => {
				e.Context.Log.Add(new LogEntry { Message = $"Insert trigger fired for {e.Entity.FirstName} at ${DateTime.Now}"});
				Console.WriteLine("Inserting " + e.Entity.FirstName);
			};
			Triggers<Person>.Updating += e => Console.WriteLine("Updating " + e.Original.FirstName + " to " + e.Entity.FirstName);
			Triggers<Person>.Deleting += e => Console.WriteLine("Deleting " + e.Original.FirstName + " to " + e.Entity.FirstName);
			Triggers<Person>.Inserted += e => Console.WriteLine("Inserted " + e.Entity.FirstName);
			Triggers<Person>.Updated  += e => Console.WriteLine("Updated " + e.Entity.FirstName);
			Triggers<Person>.Deleted  += e => Console.WriteLine("Deleted " + e.Entity.FirstName);
		}

		private static void Main(String[] args) => Task.WaitAll(MainAsync(args));

		private static async Task MainAsync(String[] args) {
			using (var context = new Context()) {
				context.Database.Delete();
				context.Database.Create();

				var log = context.Log.ToList();

				var nickStrupat = new Person {
					FirstName = "Nick",
					LastName = "Strupat"
				};

				context.People.Add(nickStrupat);
				Console.WriteLine(context.SaveChanges());
				log = context.Log.ToList();

				nickStrupat.FirstName = "Nicholas";
				Console.WriteLine(context.SaveChanges());

				nickStrupat.FirstName = "N.";
				context.People.Remove(nickStrupat);
				Console.WriteLine(await context.SaveChangesAsync());
				
				var peeps = context.People.ToList();
				peeps.ForEach(Console.WriteLine);
			}
		}
	}
}