// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.BotBuilderSamples.Bots;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Bot.Schema;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using IMiddleware = Microsoft.Bot.Builder.IMiddleware;
using CoreBot;

namespace Microsoft.BotBuilderSamples
{
    public class Startup
    {
        public Startup()
        {
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // Create the credential provider to be used with the Bot Framework Adapter.
            services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

            // Create the Bot Framework Adapter with error handling enabled.
            //services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
            services.AddSingleton<IBotFrameworkHttpAdapter, DefaultAdapter>();


            // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
            services.AddSingleton<IStorage, MemoryStorage>();

            // Create the User state. (Used in this bot's Dialog implementation.)
            services.AddSingleton<UserState>();

            // Create the Conversation state. (Used by the Dialog system itself.)
            services.AddSingleton<ConversationState>();

            // The Dialog that will be run by the bot.
            services.AddSingleton<MainDialog>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, DialogAndWelcomeBot<MainDialog>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            //app.UseHttpsRedirection();
            app.UseWebSockets();
            app.UseMvc();
        }
    }

    //public class DefaultAdapter : AdapterBase
    public class DefaultAdapter : BuggyAdapterBase
    {
        public DefaultAdapter(
            ICredentialProvider credentialProvider)
            : base(credentialProvider)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                await turnContext.SendActivityAsync(new Activity(type: ActivityTypes.Trace, text: $"{exception.Message}"));
                await turnContext.SendActivityAsync(new Activity(type: ActivityTypes.Trace, text: $"{exception.StackTrace}"));
                await turnContext.SendActivityAsync("an Error Occurred");
            };

            //Use(new MyTypingMiddleware());
            Use(new ShowTypingMiddleware());
        }
    }

    public class AdapterBase : BotAdapter, IBotFrameworkHttpAdapter
    {
        private readonly BotFrameworkHttpAdapter _botFrameworkHttpAdapter;
        private Lazy<bool> _ensureMiddlewareSet;
        private readonly object initLock = new object();

        private readonly List<Bot.Builder.IMiddleware> middlewares = new List<Bot.Builder.IMiddleware>();

        public AdapterBase(ICredentialProvider credentialProvider = null, IChannelProvider channelProvider = null, ILoggerFactory loggerFactory = null)
        {
            _botFrameworkHttpAdapter = new BotFrameworkHttpAdapter(credentialProvider, channelProvider, loggerFactory?.CreateLogger<BotFrameworkHttpAdapter>());
            _ensureMiddlewareSet = new Lazy<bool>(() =>
            {
                middlewares.ForEach(mw => _botFrameworkHttpAdapter.Use(mw));
                return true;
            });
        }

        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool _ = _ensureMiddlewareSet.Value;
            await _botFrameworkHttpAdapter.ProcessAsync(httpRequest, httpResponse, bot, cancellationToken).ConfigureAwait(false);
        }

        public new AdapterBase Use(IMiddleware middleware)
        {
            lock (initLock)
            {
                middlewares.Add(middleware);
            }
            return this;
        }

        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public class BuggyAdapterBase : BotAdapter, IBotFrameworkHttpAdapter
    {
        private readonly BotFrameworkHttpAdapter _botFrameworkHttpAdapter;
        private Lazy<bool> _ensureMiddlewareSet;
        private readonly object initLock = new object();

        private readonly List<Bot.Builder.IMiddleware> middlewares = new List<Bot.Builder.IMiddleware>();

        public BuggyAdapterBase(ICredentialProvider credentialProvider = null, IChannelProvider channelProvider = null, ILoggerFactory loggerFactory = null)
        {
            _botFrameworkHttpAdapter = new BotFrameworkHttpAdapter(credentialProvider, channelProvider, loggerFactory?.CreateLogger<BotFrameworkHttpAdapter>());
            _ensureMiddlewareSet = new Lazy<bool>(() =>
            {
                _botFrameworkHttpAdapter.Use(base.MiddlewareSet);
                return true;
            });
        }

        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool _ = _ensureMiddlewareSet.Value;
            await _botFrameworkHttpAdapter.ProcessAsync(httpRequest, httpResponse, bot, cancellationToken).ConfigureAwait(false);
        }

        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
