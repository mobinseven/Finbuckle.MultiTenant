//    Copyright 2018 Andrew White
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Options;
using Finbuckle.MultiTenant.Stores;
using Finbuckle.MultiTenant.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provices builder methods for Finbuckle.MultiTenant services and configuration.
    /// </summary>

    public static class FinbuckleMultiTenantBuilderExtensions
    {
        /// <summary>
        /// Adds a HttpRemoteSTore to the application.
        /// </summary>
        /// <param name="endpointTemplate">The endpoint URI template.</param>
        /// <param name="clientConfig">An action to configure the underlying HttpClient.</param>
        public static FinbuckleMultiTenantBuilder WithHttpRemoteStore(this FinbuckleMultiTenantBuilder builder,
                                                                      string endpointTemplate)
        => builder.WithHttpRemoteStore(endpointTemplate, null);

        /// <summary>
        /// Adds a HttpRemoteSTore to the application.
        /// </summary>
        /// <param name="endpointTemplate">The endpoint URI template.</param>
        /// <param name="clientConfig">An action to configure the underlying HttpClient.</param>
        public static FinbuckleMultiTenantBuilder WithHttpRemoteStore(this FinbuckleMultiTenantBuilder builder,
                                                                      string endpointTemplate,
                                                                      Action<IHttpClientBuilder> clientConfig)
        {
            var httpClientBuilder = builder.Services.AddHttpClient(typeof(HttpRemoteStoreClient).FullName);
            if(clientConfig != null)
                clientConfig(httpClientBuilder);

            builder.Services.TryAddSingleton<HttpRemoteStoreClient>();

            return builder.WithStore<HttpRemoteStore>(ServiceLifetime.Singleton, endpointTemplate);
        }

        /// <summary>
        /// Adds a ConfigurationStore to the application. Uses the default IConfiguration and section "Finbuckle:MultiTenant:Stores:ConfigurationStore".
        /// </summary>
        public static FinbuckleMultiTenantBuilder WithConfigurationStore(this FinbuckleMultiTenantBuilder builder)
            => builder.WithStore<ConfigurationStore>(ServiceLifetime.Singleton);
        
        /// <summary>
        /// Adds a ConfigurationStore to the application.
        /// </summary>
        /// <param name="configuration">The IConfiguration to load the section from.</param>
        /// <param name="sectionName">The configuration section to load.</param>
        public static FinbuckleMultiTenantBuilder WithConfigurationStore(this FinbuckleMultiTenantBuilder builder,
                                                                         IConfiguration configuration,
                                                                         string sectionName)
            => builder.WithStore<ConfigurationStore>(ServiceLifetime.Singleton, configuration, sectionName);

        /// <summary>
        /// Adds an empty, case-insensitive InMemoryStore to the application.
        /// </summary>
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder)
            => builder.WithInMemoryStore(true);

        /// <summary>
        /// Adds an empty InMemoryStore to the application.
        /// </summary>
        /// <param name="ignoreCase">Whether the store should ignore case.</param>
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder,
                                                                    bool ignoreCase)
            => builder.WithInMemoryStore(_ => { }, ignoreCase);

        /// <summary>
        /// Adds and configures a case-insensitive InMemoryStore to the application using the provided ConfigurationSeciont.
        /// </summary>
        /// <param name="config">The ConfigurationSection which contains the InMemoryStore configuartion settings.</param>
        [Obsolete("Consider using ConfigurationStore instead.")]
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder,
                                                                    IConfigurationSection configurationSection)
            => builder.WithInMemoryStore(o => configurationSection.Bind(o), true);

        /// <summary>
        /// Adds and configures InMemoryStore to the application using the provided ConfigurationSeciont.
        /// </summary>
        /// <param name="config">The ConfigurationSection which contains the InMemoryStore configuartion settings.</param>
        /// <param name="ignoreCase">Whether the store should ignore case.</param>
        [Obsolete("Consider using ConfigurationStore instead.")]
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder,
                                                                    IConfigurationSection configurationSection,
                                                                    bool ignoreCase)
            => builder.WithInMemoryStore(o => configurationSection.Bind(o), ignoreCase);

        /// <summary>
        /// Adds and configures a case-insensitive InMemoryStore to the application using the provided action.
        /// </summary>
        /// <param name="config">A delegate or lambda for configuring the tenant.</param>
        /// <param name="ignoreCase">Whether the store should ignore case.</param>
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder,
                                                                    Action<InMemoryStoreOptions> config)
            => builder.WithInMemoryStore(config, true);

        /// <summary>
        /// Adds and configures InMemoryStore to the application using the provided action.
        /// </summary>
        /// <param name="config">A delegate or lambda for configuring the tenant.</param>
        /// <param name="ignoreCase">Whether the store should ignore case.</param>
        public static FinbuckleMultiTenantBuilder WithInMemoryStore(this FinbuckleMultiTenantBuilder builder,
                                                                    Action<InMemoryStoreOptions> config,
                                                                    bool ignoreCase)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.WithStore<InMemoryStore>(ServiceLifetime.Singleton, sp => InMemoryStoreFactory(config, ignoreCase));
        }

        // TODO: Clean up any "Configuration" stuff here once it is no longer supported
        private static InMemoryStore InMemoryStoreFactory(Action<InMemoryStoreOptions> config, bool ignoreCase)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var options = new InMemoryStoreOptions();
            config(options);
            var store = new InMemoryStore(ignoreCase);

            try
            {
                foreach (var tenantConfig in options.TenantConfigurations ?? new InMemoryStoreOptions.TenantConfiguration[0])
                {
                    if (string.IsNullOrWhiteSpace(tenantConfig.Id) ||
                        string.IsNullOrWhiteSpace(tenantConfig.Identifier))
                        throw new MultiTenantException("Tenant Id and Identifer cannot be null or whitespace.");

                    var tenantInfo = new TenantInfo(tenantConfig.Id,
                                               tenantConfig.Identifier,
                                               tenantConfig.Name,
                                               tenantConfig.ConnectionString ?? options.DefaultConnectionString,
                                               null);

                    foreach (var item in tenantConfig.Items ?? new Dictionary<string, string>())
                    {
                        tenantInfo.Items.Add(item.Key, item.Value);
                    }

                    if (!store.TryAddAsync(tenantInfo).Result)
                        throw new MultiTenantException($"Unable to add {tenantInfo.Identifier} because it is already present.");
                }
            }
            catch (Exception e)
            {
                throw new MultiTenantException("Unable to create ImMemoryStore from configuration.", e);
            }

            return store;
        }

        /// <summary>
        /// Adds and configures a StaticStrategy to the application.
        /// </summary>
        /// <param name="identifier">The tenant identifier to use for all tenant resolution.</param>
        public static FinbuckleMultiTenantBuilder WithStaticStrategy(this FinbuckleMultiTenantBuilder builder,
                                                                     string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Invalid value for \"identifier\"", nameof(identifier));
            }

            return builder.WithStrategy<StaticStrategy>(ServiceLifetime.Singleton, new object[] { identifier }); ;
        }

        /// <summary>
        /// Adds and configures a DelegateStrategy to the application.
        /// </summary>
        /// <param name="doStrategy">The delegate implementing the strategy.</returns>
        public static FinbuckleMultiTenantBuilder WithDelegateStrategy(this FinbuckleMultiTenantBuilder builder,
                                                                       Func<object, Task<string>> doStrategy)
        {
            if (doStrategy == null)
            {
                throw new ArgumentNullException(nameof(doStrategy));
            }

            return builder.WithStrategy<DelegateStrategy>(ServiceLifetime.Singleton, new object[] { doStrategy });
        }

        /// <summary>
        /// Adds and configures a fallback strategy for if the main strategy or remote authentication
        /// fail to resolve a tenant.
        /// </summary>
        public static FinbuckleMultiTenantBuilder WithFallbackStrategy(this FinbuckleMultiTenantBuilder builder,
                                                                       string identifier)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            builder.Services.TryAddSingleton<FallbackStrategy>(sp => new FallbackStrategy(identifier));

            return builder;
        }
    }
}