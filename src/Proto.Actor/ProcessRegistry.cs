﻿// -----------------------------------------------------------------------
//  <copyright file="ProcessRegistry.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace Proto
{
    public class ProcessRegistry
    {
        private const string NoHost = "nonhost";
        private readonly IList<Func<PID, ActorRef>> _hostResolvers = new List<Func<PID, ActorRef>>();

        private readonly HashedConcurrentDictionary _localActorRefs =
            new HashedConcurrentDictionary();

        private int _sequenceId;
        public static ProcessRegistry Instance { get; } = new ProcessRegistry();

        public string Address { get; set; } = NoHost;

        public void RegisterHostResolver(Func<PID, ActorRef> resolver)
        {
            _hostResolvers.Add(resolver);
        }

        public ActorRef Get(PID pid)
        {
            if (pid.Address != "nonhost" && pid.Address != Address)
            {
                foreach (var resolver in _hostResolvers)
                {
                    var reff = resolver(pid);
                    if (reff == null)
                    {
                        continue;
                    }
                    //this is racy but it doesnt matter
                    pid.Ref = reff;
                    return reff;
                }
                throw new NotSupportedException("Unknown host");
            }

            ActorRef aref;
            if (_localActorRefs.TryGetValue(pid.Id, out aref))
            {
                return aref;
            }
            return DeadLetterActorRef.Instance;
        }

        public ValueTuple<PID, bool> TryAdd(string id, ActorRef aref)
        {
            var pid = new PID
            {
                Id = id,
                Ref = aref, //cache aref lookup
                Address = Address // local
            };
            var ok = _localActorRefs.TryAdd(pid.Id, aref);
            return ValueTuple.Create(pid, ok);
        }

        public void Remove(PID pid)
        {
            _localActorRefs.Remove(pid.Id);
        }

        public string NextId()
        {
            var counter = Interlocked.Increment(ref _sequenceId);
            return "$" + counter;
        }
    }
}