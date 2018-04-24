﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Prism.Navigation;
using Prism.Segue.Application.ViewModels;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Forms.Xaml.Internals;

namespace Prism.Segue.Application.Segue
{
    [ContentProperty(nameof(UseModalNavigation))]
    public class Uri : IMarkupExtension, ICommand
    {
        public static readonly BindableProperty NavigationParametersProperty =
            BindableProperty.CreateAttached ("NavigationParameters", typeof(NavigationParameters), typeof(Uri), null);

        public static NavigationParameters GetNavigationParameters (BindableObject view)
        {
            return (NavigationParameters)view.GetValue (NavigationParametersProperty);
        }

        public static void SetNavigationParameters (BindableObject view, NavigationParameters value)
        {
            view.SetValue (NavigationParametersProperty, value);
        }

        private bool _navigating;
        private INavigationService _navService;
        private IRootObjectProvider _rootObjectProvider;
        private SimpleValueTargetProvider _valueTargetProvider;
        private NavigationParameters _navigationParameters;
        public bool AllowDoubleTap { get; set; } = false;
        public bool Animated { get; set; } = true;
        public UseModalNavigation UseModalNavigation { get; set; } = UseModalNavigation.Auto;

        public bool CanExecute(object parameter)
        {
            return AllowDoubleTap || !_navigating;
        }

        public event EventHandler CanExecuteChanged;

        public async void Execute(object parameter)
        {
            _navigating = true;
            RaiseCanExecuteChanged();
            InitNavService();
            
            if (_navService != null)
            {
                bool? useModalNavigation;
                switch (UseModalNavigation)
                {
                    case UseModalNavigation.Modal:
                        useModalNavigation = true;
                        break;
                    case UseModalNavigation.Hierarchical:
                        useModalNavigation = false;
                        break;
                    case UseModalNavigation.BackToRoot:
                        await _navService.GoBackToRootAsync(_navigationParameters);
                        return;
                    case UseModalNavigation.GoBack:
                        await _navService.GoBackAsync(_navigationParameters);
                        return;
                    case UseModalNavigation.Auto:
                    default:
                        useModalNavigation = null;
                        break;
                }

                switch (parameter)
                {
                    case System.Uri uri:
                        await _navService.NavigateAsync(uri,_navigationParameters, useModalNavigation, Animated);
                        break;
                    default:
                        await _navService.NavigateAsync(parameter?.ToString(),_navigationParameters, useModalNavigation, Animated);
                        break;
                }
            }

            _navigating = false;
            RaiseCanExecuteChanged();
        }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");

            _rootObjectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            _valueTargetProvider = serviceProvider.GetService(typeof(IProvideValueTarget)) as SimpleValueTargetProvider;

            return this;
        }

        private void InitNavService()
        {
            if (_navService != null) return;
            // if XamlCompilation is active, IRootObjectProvider is not available, but SimpleValueTargetProvider is available
            // if XamlCompilation is inactive, IRootObjectProvider is available, but SimpleValueTargetProvider is not available
            object rootObject;
            object segueItem = null;
            if (_rootObjectProvider == null && _valueTargetProvider == null)
                throw new ArgumentException("serviceProvider does not provide an IRootObjectProvider or SimpleValueTargetProvider");
            if (_rootObjectProvider == null)
            {
                PropertyInfo propertyInfo = _valueTargetProvider.GetType().GetTypeInfo().DeclaredProperties.FirstOrDefault(dp => dp.Name.Contains("ParentObjects"));
                if (propertyInfo == null) throw new ArgumentNullException("ParentObjects");

                var parentObjects = (propertyInfo.GetValue(_valueTargetProvider) as IEnumerable<object>).ToList();
                var parentObject = parentObjects.FirstOrDefault(pO => pO.GetType().GetTypeInfo().IsSubclassOf(typeof(Page)));

                segueItem = parentObjects.FirstOrDefault();
                rootObject = parentObject ?? throw new ArgumentNullException("parentObject");
            }
            else
            {
                rootObject = _rootObjectProvider.RootObject;
            }

            if (rootObject is Page page && page.BindingContext is ViewModelBase vm) _navService = vm.NavigationService;
            if (segueItem != null && segueItem is BindableObject bindable)
            {
                _navigationParameters = GetNavigationParameters(bindable);
            }
        }

        private void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}