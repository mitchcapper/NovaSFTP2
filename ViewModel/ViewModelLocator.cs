using System.Net;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;

namespace NovaSFTP2.ViewModel {
	class ViewModelLocator {
		private static ViewModelLocator _instance;
		public static ViewModelLocator instance {
			get { return _instance ?? (_instance = new ViewModelLocator()); }
		}
		public ViewModelLocator()//Doesn't seem this is actually used.
		{
			ServicePointManager.DefaultConnectionLimit = 25;
			bool first = _instance == null;
			_instance = this;
			ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
			if (first)
				SimpleIoc.Default.Register<MainViewModel>();

		}
		public MainViewModel Main {
			get {
				return ServiceLocator.Current.GetInstance<MainViewModel>();
			}
		}
	}
}
