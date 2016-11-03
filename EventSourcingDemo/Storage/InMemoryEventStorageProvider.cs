﻿using EventSourcingDemo.Domain;
using EventSourcingDemo.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using NEventLite.Domain;
using NEventLite.Events;
using NEventLite.Exceptions;
using NEventLite.Storage;

namespace EventSourcingDemo.Storage
{
    class InMemoryEventStorageProvider : IEventStorageProvider
    {
        private Dictionary<Guid, List<Event>> eventStream = new Dictionary<Guid, List<Event>>();

        public bool HasConcurrencyCheck => false;

        public IEnumerable<Event> GetEvents(Guid aggregateId, int start, int count)
        {
            try
            {
                if (eventStream.ContainsKey(aggregateId))
                {

                    //this is needed for make sure it doesn't fail when we have int.maxValue for count
                    if (count > int.MaxValue - start)
                    {
                        count = int.MaxValue - start;
                    }

                    return
                        eventStream[aggregateId].Where(
                            o =>
                                (eventStream[aggregateId].IndexOf(o) >= start) &&
                                (eventStream[aggregateId].IndexOf(o) < (start + count)))
                            .ToArray();
                }
                else
                {
                    return new List<Event>();
                }
                
            }
            catch (Exception ex)
            {
                throw new AggregateNotFoundException($"The aggregate with {aggregateId} was not found. Details {ex.Message}");
            }

        }

        public void CommitChanges(AggregateRoot aggregate)
        {
            var events = aggregate.GetUncommittedChanges();

            if (events.Any())
            {
                if (eventStream.ContainsKey(aggregate.Id) == false)
                {
                    eventStream.Add(aggregate.Id, events.ToList());
                }
                else
                {
                    eventStream[aggregate.Id].AddRange(events);
                }
            }

        }
    }
}
