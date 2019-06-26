﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace CoreBot
{
    public class MyTypingMiddleware : IMiddleware
    {
        private readonly TimeSpan _delay;
        private readonly TimeSpan _period;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShowTypingMiddleware"/> class.
        /// </summary>
        /// <param name="delay">Initial delay before sending first typing indicator. Defaults to 500ms.</param>
        /// <param name="period">Rate at which additional typing indicators will be sent. Defaults to every 2000ms.</param>
        public MyTypingMiddleware(int delay = 500, int period = 2000)
        {
            if (delay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be greater than or equal to zero");
            }

            if (period <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "Repeat period must be greater than zero");
            }

            _delay = TimeSpan.FromMilliseconds(delay);
            _period = TimeSpan.FromMilliseconds(period);
        }

        /// <summary>
        /// Processess an incoming activity.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Spawns a thread that sends the periodic typing activities until the turn ends.
        /// </remarks>
        /// <seealso cref="ITurnContext"/>
        /// <seealso cref="Bot.Schema.IActivity"/>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken)
        {
            CancellationTokenSource cts = null;
            try
            {
                // If the incoming activity is a MessageActivity, start a timer to periodically send the typing activity
                if (turnContext.Activity.Type == ActivityTypes.Message)
                {
                    cts = new CancellationTokenSource();
                    cancellationToken.Register(() => cts.Cancel());

                    // do not await task - we want this to run in thw background and we wil cancel it when its done
                    var task = Task.Run(() => SendTypingAsync(turnContext, _delay, _period, cts.Token), cancellationToken);
                }

                await next(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (cts != null)
                {
                    cts.Cancel();
                }
            }
        }

        private static async Task SendTypingAsync(ITurnContext turnContext, TimeSpan delay, TimeSpan period, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await SendTypingActivityAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    }

                    // if we happen to cancel when in the delay we will get a TaskCanceledException
                    await Task.Delay(period, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // do nothing
            }
        }

        private static async Task SendTypingActivityAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // create a TypingActivity, associate it with the conversation and send immediately
            var typingActivity = new Activity
            {
                Type = ActivityTypes.Typing,
                RelatesTo = turnContext.Activity.RelatesTo,
            };

            // sending the Activity directly on the Adapter avoids other Middleware and avoids setting the Responded
            // flag, however, this also requires that the conversation reference details are explicitly added.
            var conversationReference = turnContext.Activity.GetConversationReference();
            typingActivity.ApplyConversationReference(conversationReference);

            // make sure to send the Activity directly on the Adapter rather than via the TurnContext
            await turnContext.Adapter.SendActivitiesAsync(turnContext, new Activity[] { typingActivity }, cancellationToken).ConfigureAwait(false);
        }
    }
}
