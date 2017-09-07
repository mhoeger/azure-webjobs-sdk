// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// This class provides factory methods for the creation of instances
    /// used for ServiceBus message processing.
    /// </summary>
    public class MessagingProvider
    {
        private readonly ServiceBusConfiguration _config;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="config">The <see cref="ServiceBusConfiguration"/>.</param>
        public MessagingProvider(ServiceBusConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;
        }

        /// <summary>
        /// Creates a <see cref="MessageProcessor"/> for the specified ServiceBus entity.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageProcessor"/> for.</param>
        /// <returns>The <see cref="MessageProcessor"/>.</returns>
        public virtual MessageProcessor CreateMessageProcessor(string entityPath)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            return new MessageProcessor(_config.MessageOptions);
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageReceiver"/>.
        /// </remarks>
        /// <param name="factory">The <see cref="MessagingFactory"/> to use.</param>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <returns></returns>
        public virtual MessageReceiver CreateMessageReceiver(string entityPath)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }

            // TODO: FACAVAL - Create receiver, can't rely on the factory anymore
            //MessageReceiver receiver =  factory.CreateMessageReceiver(entityPath);
            //receiver.PrefetchCount = _config.PrefetchCount;
            //return receiver;

            return null;
        }

        /// <summary>
        /// Gets the connection string for the specified connection string name.
        /// If no value is specified, the default connection string will be returned.
        /// </summary>
        /// <param name="connectionStringName">The connection string name.</param>
        /// <returns>The ServiceBus connection string.</returns>
        protected internal string GetConnectionString(string connectionStringName = null)
        {
            string connectionString = _config.ConnectionString;
            if (!string.IsNullOrEmpty(connectionStringName))
            {
                connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(connectionStringName);
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "Microsoft Azure WebJobs SDK ServiceBus connection string '{0}{1}' is missing or empty.",
                    "AzureWebJobs", connectionStringName ?? ConnectionStringNames.ServiceBus));
            }

            return connectionString;
        }
    }
}
