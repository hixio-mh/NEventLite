# NEventLite [![Build status](https://ci.appveyor.com/api/projects/status/yfc041h8uvt7u3dq?svg=true)](https://ci.appveyor.com/project/dasiths/neventlite) [![NuGet](https://img.shields.io/nuget/v/NEventLite.svg)](https://www.nuget.org/packages/NEventLite) [![Downloads](https://img.shields.io/nuget/dt/NEventLite.svg)](https://www.nuget.org/packages/NEventLite/)

## An extensible lightweight library for .NET that manages the Aggregate lifecycle in an **Event Sourced** system

Supports `Event` and `Snapshot` storage providers like EventStore/Redis or SQL Server. Built with dependency injection in mind and seamlessly integrates with AspNetCore.

## Give a star :star: if you appreciate the effort

## What is Event Sourcing?

> Use an append-only store to record the full series of events that describe actions taken on data in a domain, rather than storing just the current state.

Start here [https://dasith.me/2016/12/02/event-sourcing-examined-part-1-of-3/](https://dasith.me/2016/12/02/event-sourcing-examined-part-1-of-3/)

## What does NEventLite solve? :goal_net:
NEventLite makes it easier to implement the event sourcing pattern in your .NET project. The library is opinionated and introduces some patterns to manage the life cycle of your [Aggregates](https://martinfowler.com/bliki/DDD_Aggregate.html) in an event sourced system. It will manage Creating, Loading, Mutating and Persisting Aggregates and Events.

## What doesn't it solve? :warning:

NEventLite is **not a framework** that manages your application end to end. It doesn't enforce ports and adapters pattern or any of the application level concerns. The aim is to do one thing (Manage aggregate lifecycle) and do that well. If you need to implement command and event handlers you can have a look at something like [SimpleMediator](https://github.com/dasiths/SimpleMediator) or [Brighter](https://github.com/BrighterCommand/Brighter) and NEventLite will complement them nicely.

## What about v1.0? Wasn't it advertised as a framework? 
*NEventLite V1.0 tried to solve similar problems but the scope of the project very large and it was decided to narrow down the scope. If you're still looking for reference it's hosted [here](https://github.com/dasiths/NEventLite/blob/master/legacy/v1.0).*

## Before you start

- The library targets .NET Standard 2.0
- *Optional:* Installation of EventStore - [https://eventstore.com/](https://eventstore.com/) (You can use the in memory event and snapshot providers when developing)

## Using It

Define the events. They are simple pocos that will be serialized and stored in `EventStorage` when changes are saved. Events use `Guid` for Id by *default* but they can be changed to use any data type as Id. See `Event<TAggregateKey, TEventKey>` for reference.

```csharp
    public class ScheduleCreatedEvent : Event<Schedule>
    {
        public string ScheduleName { get; set; }

        public ScheduleCreatedEvent(Guid scheduleId, string scheduleName) : base(Guid.NewGuid(), scheduleId)
        {
            ScheduleName = scheduleName;
        }
    }

    public class TodoCreatedEvent : Event<Schedule>
    {
        public Guid TodoId { get; set; }
        public string Text { get; set; }

        public TodoCreatedEvent(Guid aggregateId, int targetVersion, Guid todoId, string text) : base(Guid.NewGuid(), aggregateId, targetVersion)
        {
            TodoId = todoId;
            Text = text;
        }
    }
```

Define the Aggregate (`Schedule.cs` in the sample). Aggregates use `Guid` as Id by *default* but just like the events, they can be changed to the Id type of your choice. See `AggregateRoot<TAggregateKey>` for reference.

```csharp
    public class Schedule : AggregateRoot
    {
        public IList<Todo> Todos { get; private set; }
        public string ScheduleName { get; private set; }

        // To create or mutate, call the constructor or methods.

        // Constructor
        public Schedule(string scheduleName)
        {
            // Pattern: Create an event and apply it
            var newScheduleId = Guid.NewGuid();
            var @event = new ScheduleCreatedEvent(newScheduleId, scheduleName);
            ApplyEvent(@event);
        }

        public Guid AddTodo(string text)
        {
            // Pattern: Create an event and apply it
            var newTodoId = Guid.NewGuid();
            var @event = new TodoCreatedEvent(Id, CurrentVersion, newTodoId, text);
            ApplyEvent(@event);
            return newTodoId;
        }

        // ** Mutations happen through applying events **
        // The library identifies the internal event handler methods though a special method attribute.

        [InternalEventHandler]
        public void OnScheduleCreated(ScheduleCreatedEvent @event)
        {
            ScheduleName = @event.ScheduleName;
            Todos = new List<Todo>();
        }

        [InternalEventHandler]
        public void OnTodoCreated(TodoCreatedEvent @event)
        {
            var todo = new Todo(@event.TodoId, @event.Text);
            Todos.Add(todo);
        }
    }
```

Using the built in `Session` and `Repository` implementations to manage the Aggregate lifecycle.

```csharp
// We recommend using a DI container to inject the Session. Keep it scoped (per request in a web application)

    public class CreateScheduleHandler
    {
        private readonly ISession<Schedule> _session;

        public CreateScheduleCommandHandler(ISession<Schedule> session)
        {
            _session = session;
        }

        public async Task<Guid> HandleAsync()
        {
            var schedule = new Schedule("my new schedule");
            _session.Attach(schedule);
            await _session.SaveAsync();
            return schedule.Id;
        }
    }

    public class AddTodoHandler
    {
        private readonly ISession<Schedule> _session;

        public AddTodoCommandHandler(ISession<Schedule> session)
        {
            _session = session;
        }

        public async Task<Guid> HandleAsync(Guid scheduleId)
        {
            var schedule = await _session.GetByIdAsync(scheduleId);
            var id = schedule.AddTodo("test todo 1");
            await _session.SaveAsync();
            return id;
        }
    }
```

## Dependency Injection

The library is built with DI as a first class concept. Wiring it up is easy. This is an example of how you would do it with `Microsoft.Extensions.DependencyInjection`. You can find a detailed [example in the sample console app](https://github.com/dasiths/NEventLite/blob/master/src/Samples/NEventLite.Samples.ConsoleApp/DependencyInjection.cs).

```csharp
    public static ServiceProvider GetContainer() {
        var services = new ServiceCollection();

        // Wiring up the event storage provider. "https://eventstore.com/" in this example
        services.AddScoped<IEventStoreStorageConnectionProvider, EventStoreStorageConnectionProvider>();
        services.AddScoped<IEventStorageProvider, EventStoreEventStorageProvider>();
        services.AddScoped<ISnapshotStorageProvider, EventStoreSnapshotStorageProvider>();

        // Register the repository
        services.AddScoped<IRepository<Schedule>, Repository<Schedule, ScheduleSnapshot>>();
        // If you prefer to work without snapshots and use events only repository
        // services.AddScoped<IRepository<Schedule>, EventOnlyRepository<Schedule>>();

        // register the session implementation for the Aggregate
        services.AddScoped<ISession<Schedule>, Session<Schedule>>();

        // Or
        // Instead of specifying each Aggregate and Snapshot type you can use the convenience extension method "ScanAndRegisterAggregates()" in the "NEventLite.Extensions.Microsoft.DependencyInjection" nuget library as shown below

        //services.ScanAndRegisterAggregates();

        // Use the defaults
        services.AddSingleton<IClock, DefaultSystemClock>();
        services.AddSingleton<IEventPublisher, DefaultNoOpEventPublisher>();

        // Or 
        // use your own implementation
        // services.AddSingleton<IClock, MyClock>();
        // services.AddSingleton<IEventPublisher, MyEventPublisher>();

        var container = services.BuildServiceProvider();
        return container;
    }
```

If you want to use it with a different dependency injection framework, you can look at how the assembly scanning and registration is implemented for `Microsoft.Extensions.DependencyInjection` as an example and come up with your own implementation.

```csharp
        public static void ScanAndRegisterAggregates(this ServiceCollection services, IList<Assembly> assemblies)
        {
            var allAggregates = assemblies
                .SelectMany(a =>
                    a.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract & typeof(IAggregateRoot).IsAssignableFrom(t)))
                .ToList();

            var aggregates = allAggregates.Select(a =>
            {
                var interfaces = a.GetInterfaces();
                var snapshottable = interfaces.FirstOrDefault(i =>
                    i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ISnapshottable<>) ||
                                        i.GetGenericTypeDefinition() == typeof(ISnapshottable<,,>)));

                return new KeyValuePair<Type, Type>(a, snapshottable?.GetGenericArguments()[0]);
            }).ToList();

            foreach (var kvp in aggregates)
            {
                var aggregateType = kvp.Key;
                var snapshotType = kvp.Value;

                services.AddScoped(typeof(IRepository<>).MakeGenericType(aggregateType),
                    snapshotType != null
                        ? typeof(Repository<,>).MakeGenericType(aggregateType, snapshotType)
                        : typeof(EventOnlyRepository<>).MakeGenericType(aggregateType));

                services.AddScoped(typeof(ISession<>).MakeGenericType(aggregateType),
                    typeof(Session<>).MakeGenericType(aggregateType));
            }
        }
```

## Storage providers

The library contains storage provider implementation for [EventSore](https://eventstore.com/) and we plan to include a few more in the future. We have also included an in memory event and snapshot storage provider to get you up and running faster.

It's very easy to implement your own as well. Implement `IEventStorageProvider` and `ISnapshotStorageProvider` interfaces an you're good to plug them in. If you need help look at the `NEventLite.StorageProviders.EventStore` project in the repository.

```csharp
    // Interface for persisting and reading events
    public interface IEventStorageProvider<TAggregateKey, TEventKey>
    {
        Task<IEnumerable<IEvent<AggregateRoot<TAggregateKey, TEventKey>, TAggregateKey, TEventKey>>> GetEventsAsync<TAggregate>(TAggregateKey aggregateId, int start, int count)
            where TAggregate : AggregateRoot<TAggregateKey, TEventKey>;

        Task<IEvent<AggregateRoot<TAggregateKey, TEventKey>, TAggregateKey, TEventKey>> GetLastEventAsync<TAggregate>(TAggregateKey aggregateId)
            where TAggregate : AggregateRoot<TAggregateKey, TEventKey>;

        Task SaveAsync<TAggregate>(TAggregate aggregate) where TAggregate: AggregateRoot<TAggregateKey, TEventKey>;
    }

    // Interface for persisting and reading snapshots
    public interface ISnapshotStorageProvider<in TAggregateKey, in TSnapshotKey>
    {
        int SnapshotFrequency { get; }

        Task<TSnapshot> GetSnapshotAsync<TSnapshot>(TAggregateKey aggregateId) where TSnapshot : ISnapshot<TAggregateKey, TSnapshotKey>;

        Task SaveSnapshotAsync<TSnapshot>(TSnapshot snapshot) where TSnapshot : ISnapshot<TAggregateKey, TSnapshotKey>;
    }
```

## Examples

There are more examples in the [Samples folder](https://github.com/dasiths/NEventLite/tree/master/src/Samples) if you need help figuring out how to put everything together.

## Notes

Please feel free to contribute and improve the code as you see fit. Please raise an issue if you find a bug or have an improvement idea. The repository is shared under the MIT license.
