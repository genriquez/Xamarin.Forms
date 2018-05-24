using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xamarin.Forms.Internals;
using Xamarin.Forms.PlatformConfiguration;
using Xamarin.Forms.PlatformConfiguration.iOSSpecific;
using Xamarin.Forms.PlatformConfiguration.WindowsSpecific;

namespace Xamarin.Forms.Controls
{

	public class App : Application
	{
		public const string AppName = "XamarinFormsControls";
		static string s_insightsKey;

		// ReSharper disable once InconsistentNaming
		public static int IOSVersion = -1;

		public static List<string> AppearingMessages = new List<string>();

		static Dictionary<string, string> s_config;
		readonly ITestCloudService _testCloudService;

		public const string DefaultMainPageId = "ControlGalleryMainPage";

		public App()
		{
			_testCloudService = DependencyService.Get<ITestCloudService>();

			SetMainPage(CreateDefaultMainPage());

			//// Uncomment to verify that there is no gray screen displayed between the blue splash and red MasterDetailPage.
			//SetMainPage(new Bugzilla44596SplashPage(() =>
			//{
			//	var newTabbedPage = new TabbedPage();
			//	newTabbedPage.Children.Add(new ContentPage { BackgroundColor = Color.Red, Content = new Label { Text = "yay" } });
			//	MainPage = new MasterDetailPage
			//	{
			//		Master = new ContentPage { Title = "Master", BackgroundColor = Color.Red },
			//		Detail = newTabbedPage
			//	};
			//}));

			//// Uncomment to verify that there is no crash when switching MainPage from MDP inside NavPage
			//SetMainPage(new Bugzilla45702());
		}

		[Preserve(AllMembers = true)]
		public class Data : INotifyPropertyChanged
		{
			private string _text;

			public string Text
			{
				get => _text;
				set
				{
					_text = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Font)));
				}
			}

			public Color Color
			{
				get
				{
					return new Color(new Random().Next(0, 255) / 255.0, new Random().Next(0, 255) / 255.0, new Random().Next(0, 255) / 255.0);
				}
			}

			public Font Font
			{
				get
				{
					return Font.SystemFontOfSize(DateTime.Now.Second);
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;
		}

		async void DoStuff(ObservableCollection<Data> dataElements)
		{
			foreach (var item in dataElements)
			{
				item.Text = Guid.NewGuid().ToString();
			}
			dataElements.Move(0, 2);
			dataElements.Move(2, 26);

			await Task.Delay(500);
			Device.BeginInvokeOnMainThread(() => DoStuff(dataElements));
		}

		public Page CreateDefaultMainPage()
		{
			return new NavigationPage(new ContentPage()
			{
				Content = new StackLayout()
				{
					Children =
					{
						new Button()
						{
							Text    = "button",
							Command = new Command(() =>
							{
								App.Current.MainPage.Navigation.PushAsync(CreateListViewPage());
							})
						}
					}
				}
			});
		}

		Page CreateListViewPage()
		{
			ListView view = new ListView(ListViewCachingStrategy.RecycleElement);
			view.ItemTemplate = new DataTemplate(() =>
			{
				ViewCell cell = new ViewCell();

				Label label = new Label();
				label.TextColor = Color.Black;
				label.BackgroundColor = Color.White;
				label.Text = "Candyt";
				label.SetBinding(Label.TextProperty, "Text");
				label.SetBinding(Label.FontProperty, "Font");
				cell.View = new ContentView()
				{
					Content = new StackLayout()
					{
						Orientation = StackOrientation.Horizontal,
						Children =
						{
							label,
							new Image{Source = "coffee.png"}
						}
					},
					HeightRequest = 40
				};

				(cell.View as ContentView).SetBinding(ContentView.BackgroundColorProperty, "Color");
				return cell;
			});

			var data = new ObservableCollection<Data>(Enumerable.Range(0, 70).Select(_ => new Data()));
			view.ItemsSource = data;
			//DoStuff(data);

			MessagingCenter.Subscribe<object>(this, "LabelRendererDispose", (x)=>
			{
				MainPage.Navigation.PopAsync();
			});

			return new ContentPage()
			{
				Content = view
			};
		}

		protected override void OnAppLinkRequestReceived(Uri uri)
		{
			var appDomain = "http://" + AppName.ToLowerInvariant() + "/";

			if (!uri.ToString().ToLowerInvariant().StartsWith(appDomain))
				return;

			var url = uri.ToString().Replace(appDomain, "");

			var parts = url.Split('/');
			if (parts.Length == 2)
			{
				var isPage = parts[0].Trim().ToLower() == "gallery";
				if (isPage)
				{
					string page = parts[1].Trim();
					var pageForms = Activator.CreateInstance(Type.GetType(page));

					var appLinkPageGallery = pageForms as AppLinkPageGallery;
					if (appLinkPageGallery != null)
					{
						appLinkPageGallery.ShowLabel = true;
						(MainPage as MasterDetailPage)?.Detail.Navigation.PushAsync((pageForms as Page));
					}
				}
			}

			base.OnAppLinkRequestReceived(uri);
		}

		public static Dictionary<string, string> Config
		{
			get
			{
				if (s_config == null)
					LoadConfig();

				return s_config;
			}
		}

		public static ContentPage MenuPage { get; set; }

		public void SetMainPage(Page rootPage)
		{
			MainPage = rootPage;
		}

		static Assembly GetAssembly(out string assemblystring)
		{
			assemblystring = typeof(App).AssemblyQualifiedName.Split(',')[1].Trim();
			var assemblyname = new AssemblyName(assemblystring);
			return Assembly.Load(assemblyname);
		}

		static void LoadConfig()
		{
			s_config = new Dictionary<string, string>();

			string keyData = LoadResource("controlgallery.config").Result;
			string[] entries = keyData.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			foreach (string entry in entries)
			{
				string[] parts = entry.Split(':');
				if (parts.Length < 2)
					continue;

				s_config.Add(parts[0].Trim(), parts[1].Trim());
			}
		}

		static async Task<string> LoadResource(string filename)
		{
			string assemblystring;
			Assembly assembly = GetAssembly(out assemblystring);

			Stream stream = assembly.GetManifestResourceStream($"{assemblystring}.{filename}");
			string text;
			using (var reader = new StreamReader(stream))
				text = await reader.ReadToEndAsync();
			return text;
		}

		public bool NavigateToTestPage(string test)
		{
			try
			{
				// Create an instance of the main page
				var root = CreateDefaultMainPage();

				// Set up a delegate to handle the navigation to the test page
				EventHandler toTestPage = null;

				toTestPage = delegate (object sender, EventArgs e)
				{
					Current.MainPage.Navigation.PushModalAsync(TestCases.GetTestCases());
					TestCases.TestCaseScreen.PageToAction[test]();
					Current.MainPage.Appearing -= toTestPage;
				};

				// And set that delegate to run once the main page appears
				root.Appearing += toTestPage;

				SetMainPage(root);

				return true;
			}
			catch (Exception ex)
			{
				Log.Warning("UITests", $"Error attempting to navigate directly to {test}: {ex}");

			}

			return false;
		}

		public void Reset()
		{
			SetMainPage(CreateDefaultMainPage());
		}
	}
}