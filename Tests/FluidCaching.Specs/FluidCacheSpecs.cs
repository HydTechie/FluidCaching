﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace FluidCaching.Specs
{
    namespace FluidCacheSpecs
    {
        // TODO: When an object's maximum age is exceeded, it should be removed after a while
        // TODO: When objects are added, it should automatically clean-up
        // TODO: When new objects added rapidly through a get, it should register the cache misses per minute
        // TODO: When existing objects are returned through a get, it should register the cache hits per minute
        // TODO: When concurrently adding and removing items, it should end up being in a consistent state

        public class When_requesting_a_large_number_of_items_from_the_cache : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_requesting_a_large_number_of_items_from_the_cache()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now, null);
                    indexById = cache.AddIndex("index", user => user.Id);
                });


                When(async () =>
                {
                    foreach (int key in Enumerable.Range(0, 1000))
                    {
                        await Task.Delay(10);
                        indexById.GetItem(key.ToString(), id => new User {Id = id});
                    }
                });
            }

            [Fact]
            public void Then_the_total_number_of_items_should_match()
            {
                cache.TotalCount.Should().Be(1000);
                cache.ActualCount.Should().BeLessThan(1000);
            }
        }

        public class When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached : GivenWhenThen<User>
        {
            private DateTime now;
            private IIndex<string, User> index;
            private User theUser;
            private readonly TimeSpan minimumAge = 5.Minutes();
            private int capacity = 20;

            public When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    var cache = new FluidCache<User>(capacity, minimumAge, TimeSpan.FromHours(1), () => now, null);

                    index = cache.AddIndex("UsersById", u => u.Id, key => new User
                    {
                        Id = key,
                        Name = "key"
                    });
                });

                When(() =>
                {
                    theUser = index.GetItem("the user");

                    for (int id = 0; id < capacity; id++)
                    {
                        index.GetItem("user " + id);
                    }

                    // Forward time
                    now = now.Add(minimumAge - 1.Minutes());

                    // Trigger evaluating of the cache
                    index.GetItem("some user");

                    // Make sure any weak references are cleaned up
                    GC.Collect();

                    // Try to get the same user again.
                    return index.GetItem("the user");
                });
            }

            [Fact]
            public void Then_it_should_retain_the_user_which_minimum_age_has_not_been_reached()
            {
                Result.Should().BeSameAs(theUser);
            }
        }

        public class When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached : GivenWhenThen<User>
        {
            private DateTime now;
            private IIndex<string, User> index;
            private User theUser;
            private FluidCache<User> cache;
            private readonly TimeSpan minimumAge = 5.Minutes();
            private int capacity;

            public When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    capacity = 20;

                    cache = new FluidCache<User>(capacity, minimumAge, 1.Hours(), () => now);

                    index = cache.AddIndex("UsersById", u => u.Id, key => new User
                    {
                        Id = key,
                        Name = "key"
                    });
                });

                When(() =>
                {
                    theUser = index.GetItem("the user");

                    for (int id = 0; id < capacity; id++)
                    {
                        index.GetItem("user " + id);
                    }

                    now = now.Add(minimumAge + 1.Minutes());

                    // Trigger evaluating of the cache
                    index.GetItem("some user");

                    // Make sure any weak references are cleaned up
                    GC.Collect();

                    // Try to get the same user again.
                    return index.GetItem("the user");
                });
            }

            [Fact]
            public void Then_it_should_have_removed_the_original_object_from_the_cache_and_create_a_new_one()
            {
                Result.Should().NotBeSameAs(theUser);
            }
        }
    }

    public class User
    {
        public string Id { get; set; }

        public string Name { get; set; }
    }
}