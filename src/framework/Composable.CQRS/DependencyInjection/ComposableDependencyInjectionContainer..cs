﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Composable.Contracts;
using Composable.System;
using Composable.System.Collections.Collections;
using Composable.System.Reflection;

namespace Composable.DependencyInjection
{
    partial class ComposableDependencyInjectionContainer : IDependencyInjectionContainer, IServiceLocator, IServiceLocatorKernel
    {
        bool _createdServiceLocator;
        readonly AsyncLocal<ScopeCache?> _scopeCache = new AsyncLocal<ScopeCache?>();
        readonly Dictionary<Guid, ComponentRegistration> _registeredComponents = new Dictionary<Guid, ComponentRegistration>();

        RootCache? _rootCache;

        public IRunMode RunMode { get; }

        public IEnumerable<ComponentRegistration> RegisteredComponents() => _registeredComponents.Values.ToList();

        internal ComposableDependencyInjectionContainer(IRunMode runMode) => RunMode = runMode;

        public void Register(params ComponentRegistration[] registrations)
        {
            Assert.State.Assert(!_createdServiceLocator);

            foreach(var registration in registrations)
            {
                _registeredComponents.Add(registration.Id, registration);
            }
        }

        IServiceLocator IDependencyInjectionContainer.CreateServiceLocator()
        {
            Assert.State.Assert(!_disposed);
            if(!_createdServiceLocator)
            {
                _createdServiceLocator = true;
                _rootCache = new RootCache(_registeredComponents.Values.ToList());//Don't create in the constructor because no registrations are done and thus new component indexes will appear, thus breaking the cache.
                Verify();
            }

            return this;
        }

        void Verify()
        {
            using(((IServiceLocator)this).BeginScope())
            {
                foreach(var component in _registeredComponents.Values)
                {
                    component.Resolve(this);
                }
            }
        }

        TService[] IServiceLocator.ResolveAll<TService>() => throw new NotImplementedException();

        IDisposable IServiceLocator.BeginScope()
        {
            Assert.State.Assert(!_disposed);
            if(_scopeCache.Value != null)
            {
                throw new Exception("Scope already exists. Nested scopes are not supported.");
            }

            _scopeCache.Value = _rootCache!.CreateScopeCache();

            return Disposable.Create(EndScope);
        }

        void EndScope()
        {
            var scopeCacheValue = _scopeCache.Value;
            if(scopeCacheValue == null)
            {
                throw new Exception("Attempt to dispose scope from a context that is not within the scope.");
            }
            scopeCacheValue.Dispose();
            _scopeCache.Value = null;
        }

        [ThreadStatic] static ComponentRegistration? _parentComponent;
        public TService Resolve<TService>() where TService : class
        {
            Assert.State.Assert(!_disposed);
            var (registrations, instance) = _rootCache!.TryGetSingleton<TService>();

            if(instance is TService singleton)
            {
                return singleton;
            }

            var scopeCache = _scopeCache.Value;

            // ReSharper disable once PatternAlwaysOfType Silly ReSharper is wrong again
            if (scopeCache != null && scopeCache.TryGet<TService>() is TService scoped)
            {
                return scoped;
            }

            if(registrations == null)
            {
                throw new Exception($"No service of type: {typeof(TService).GetFullNameCompilable()} is registered.");
            }

            if(registrations.Length > 1)
            {
                throw new Exception($"Requested single instance for service:{typeof(TService)}, but there were multiple services registered.");
            }

            var currentComponent = registrations[0];

            if(_parentComponent?.Lifestyle == Lifestyle.Singleton && currentComponent.Lifestyle != Lifestyle.Singleton)
            {
                throw new Exception($"{Lifestyle.Singleton} service: {_parentComponent.ServiceTypes.First().FullName} depends on {currentComponent.Lifestyle} service: {currentComponent.ServiceTypes.First().FullName} ");
            }

            var previousResolvingComponent = _parentComponent;
            _parentComponent = currentComponent;
            lock(registrations)
            {
                try
                {
                    switch(currentComponent.Lifestyle)
                    {
                        case Lifestyle.Singleton:
                        {
                            instance = currentComponent.InstantiationSpec.FactoryMethod(this);
                            _rootCache.Set(instance, currentComponent);
                            return (TService)instance;
                        }
                        case Lifestyle.Scoped:
                        {
                            if(scopeCache == null)
                            {
                                throw new Exception("Attempted to resolve scoped component without a scope");
                            }

                            instance = currentComponent.InstantiationSpec.FactoryMethod(this);
                            scopeCache.Set(instance, currentComponent);
                            return (TService)instance;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                finally
                {
                    _parentComponent = previousResolvingComponent;
                }
            }
        }

        bool _disposed;
        public void Dispose()
        {
            if(!_disposed)
            {
                _disposed = true;
                _rootCache!.Dispose();
            }
        }
    }
}
