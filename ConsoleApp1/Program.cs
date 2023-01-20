using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

var client =
    new ServiceBusClient(
        "connectionString");

var sender = client.CreateSender("queue");
await using (var context = new EntityContext())
{
    await context.Database.MigrateAsync();
    context.Customers.RemoveRange(context.Customers);
    await context.SaveChangesAsync();

    context.Customers.Add(
        new Customer
        {
            Name = "Name",
            Description = "Desc"
        });
    await context.SaveChangesAsync();
}

var counter = 0;
while (true)
{
    await using var context = new EntityContext();
    try
    {
        Console.WriteLine(counter++);
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            var customer = context.Customers.First();

            await sender.SendMessageAsync(
                new ServiceBusMessage
                {
                    Subject = DateTime.Now.ToLongTimeString()
                });
            customer.Description = "newDesc";
            await context.SaveChangesAsync();
            transaction.Complete();
        }

        var _ = context.Customers.First().Description;
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        throw;
    }
}


public class EntityContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=my_db;Username=dev;Password=pass");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().Property(x => x.Name).IsRequired();
    }

    public DbSet<Customer> Customers { get; set; }
}


public class Customer
{
    public int CustomerID { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
}