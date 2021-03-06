﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using NuGet;
using Popcorn.Comparers;
using Popcorn.Helpers;
using Popcorn.Messaging;
using Popcorn.Services.Application;
using Popcorn.Services.Shows.Show;
using Popcorn.Services.User;

namespace Popcorn.ViewModels.Pages.Home.Show.Tabs
{
    public class SearchShowTabViewModel : ShowTabsViewModel
    {
        /// <summary>
        /// Initializes a new instance of the SearchMovieTabViewModel class.
        /// </summary>
        /// <param name="applicationService">Application state</param>
        /// <param name="showService">Show service</param>
        /// <param name="userService">The user service</param>
        public SearchShowTabViewModel(IApplicationService applicationService, IShowService showService,
            IUserService userService)
            : base(applicationService, showService, userService,
                () => LocalizationProviderHelper.GetLocalizedValue<string>("SearchTitleTab"))
        {
        }

        /// <summary>
        /// The search filter
        /// </summary>
        public string SearchFilter { get; set; }

        /// <summary>
        /// Search shows asynchronously
        /// </summary>
        public override async Task LoadShowsAsync(bool reset = false)
        {
            await LoadingSemaphore.WaitAsync();
            StopLoadingShows();
            if (reset)
            {
                Shows.Clear();
                Page = 0;
            }

            var watch = Stopwatch.StartNew();
            Page++;
            if (Page > 1 && Shows.Count == MaxNumberOfShows)
            {
                Page--;
                LoadingSemaphore.Release();
                return;
            }

            Logger.Info(
                $"Loading search page {Page} with criteria: {SearchFilter}");
            HasLoadingFailed = false;
            try
            {
                IsLoadingShows = true;
                await Task.Run(async () =>
                {
                    var result =
                        await ShowService.SearchShowsAsync(SearchFilter,
                            Page,
                            MaxNumberOfShows,
                            Genre,
                            Rating * 10,
                            CancellationLoadingShows.Token).ConfigureAwait(false);
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        Shows.AddRange(result.shows.Except(Shows, new ShowLightComparer()));
                        IsLoadingShows = false;
                        IsShowFound = Shows.Any();
                        CurrentNumberOfShows = Shows.Count;
                        MaxNumberOfShows = result.nbShows;
                        UserService.SyncShowHistory(Shows);
                    });
                }).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Page--;
                Logger.Error(
                    $"Error while loading search page {Page} with criteria {SearchFilter}: {exception.Message}");
                HasLoadingFailed = true;
                Messenger.Default.Send(new ManageExceptionMessage(exception));
            }
            finally
            {
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                Logger.Info(
                    $"Loaded search page {Page} with criteria {SearchFilter} in {elapsedMs} milliseconds.");
                LoadingSemaphore.Release();
            }
        }
    }
}