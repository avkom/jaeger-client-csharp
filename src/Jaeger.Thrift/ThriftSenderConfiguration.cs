using System;
using System.Globalization;
using Jaeger.Senders;
using Jaeger.Thrift.Senders;
using Microsoft.Extensions.Logging;

namespace Jaeger.Thrift
{
    /// <summary>
	/// Holds the configuration related to the sender. A sender can be a <see cref="HttpSender"/> or <see cref="UdpSender"/>.
	/// </summary>
	public class SenderConfiguration
    {
        private readonly ILogger _logger;

        /// <summary>
        /// A custom sender set by our consumers. If set, nothing else has effect. Optional.
        /// </summary>
        public ISender Sender { get; private set; }

        /// <summary>
        /// The Agent Host. Has no effect if the sender is set. Optional.
        /// </summary>
        public string AgentHost { get; private set; }

        /// <summary>
        /// The Agent Port. Has no effect if the sender is set. Optional.
        /// </summary>
        public int? AgentPort { get; private set; }

        /// <summary>
        /// The endpoint, like https://jaeger-collector:14268/api/traces.
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// The Auth Token to be added as "Bearer" on Authorization headers for requests sent to the endpoint.
        /// </summary>
        public string AuthToken { get; private set; }

        /// <summary>
        /// The Basic Auth username to be added on Authorization headers for requests sent to the endpoint.
        /// </summary>
        public string AuthUsername { get; private set; }

        /// <summary>
        /// The Basic Auth password to be added on Authorization headers for requests sent to the endpoint.
        /// </summary>
        public string AuthPassword { get; private set; }

        public SenderConfiguration(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Configuration>();
        }

        public SenderConfiguration WithAgentHost(string agentHost)
        {
            AgentHost = agentHost;
            return this;
        }

        public SenderConfiguration WithAgentPort(int? agentPort)
        {
            AgentPort = agentPort;
            return this;
        }

        public SenderConfiguration WithEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        public SenderConfiguration WithAuthToken(string authToken)
        {
            AuthToken = authToken;
            return this;
        }

        public SenderConfiguration WithAuthUsername(string username)
        {
            AuthUsername = username;
            return this;
        }

        public SenderConfiguration WithAuthPassword(string password)
        {
            AuthPassword = password;
            return this;
        }

        /// <summary>
        /// Returns a sender if one was given when creating the configuration, or attempts to create a sender based on the
        /// configuration's state.
        /// </summary>
        /// <returns>The sender passed via the constructor or a properly configured sender.</returns>
        public ISender GetSender()
        {
            // if we have a sender, that's the one we return
            if (Sender != null)
            {
                return Sender;
            }

            if (!string.IsNullOrEmpty(Endpoint))
            {
                HttpSender.Builder httpSenderBuilder = new HttpSender.Builder(Endpoint);
                if (!string.IsNullOrEmpty(AuthUsername) && !string.IsNullOrEmpty(AuthPassword))
                {
                    _logger.LogDebug("Using HTTP Basic authentication with data from the environment variables.");
                    httpSenderBuilder.WithAuth(AuthUsername, AuthPassword);
                }
                else if (!string.IsNullOrEmpty(AuthToken))
                {
                    _logger.LogDebug("Auth Token environment variable found.");
                    httpSenderBuilder.WithAuth(AuthToken);
                }

                _logger.LogDebug("Using the HTTP Sender to send spans directly to the endpoint.");
                return httpSenderBuilder.Build();
            }

            _logger.LogDebug("Using the UDP Sender to send spans to the agent.");
            return new UdpSender(
                    StringOrDefault(AgentHost, UdpSender.DefaultAgentUdpHost),
                    AgentPort.GetValueOrDefault(UdpSender.DefaultAgentUdpCompactPort),
                    0 /* max packet size */);
        }

        /// <summary>
        /// Attempts to create a new <see cref="SenderConfiguration"/> based on the environment variables.
        /// </summary>
        public static SenderConfiguration FromEnv(ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger<Configuration>();

            string agentHost = GetProperty(Configuration.JaegerAgentHost);
            int? agentPort = GetPropertyAsInt(Configuration.JaegerAgentPort, logger);

            string collectorEndpoint = GetProperty(Configuration.JaegerEndpoint);
            string authToken = GetProperty(Configuration.JaegerAuthToken);
            string authUsername = GetProperty(Configuration.JaegerUser);
            string authPassword = GetProperty(Configuration.JaegerPassword);

            return new SenderConfiguration(loggerFactory)
                .WithAgentHost(agentHost)
                .WithAgentPort(agentPort)
                .WithEndpoint(collectorEndpoint)
                .WithAuthToken(authToken)
                .WithAuthUsername(authUsername)
                .WithAuthPassword(authPassword);
        }

        private static string GetProperty(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        private static int? GetPropertyAsInt(string name, ILogger logger)
        {
            string value = GetProperty(name);
            if (!string.IsNullOrEmpty(value))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    return intValue;
                }
                else
                {
                    logger.LogError("Failed to parse integer for property {property} with value {value}", name, value);
                }
            }
            return null;
        }

        private static string StringOrDefault(string value, string defaultValue)
        {
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }
    }
}
