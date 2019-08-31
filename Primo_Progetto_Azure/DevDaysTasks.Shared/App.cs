using Xamarin.Forms;

namespace DevDaysTasks
{
    public class App : Application
    {
        public App()
        {
            MainPage = new NavigationPage(new TodoList())
            { BarBackgroundColor = Color.FromHex("#5ABAFF"), BarTextColor = Color.White };
        }

        protected override void OnStart()
        {
            // gestire all'avvio dell'app
        }

        protected override void OnSleep()
        {
            // gestire quando l'app è in sospensione
        }

        protected override void OnResume()
        {
            // gestire la ripresa dell'app
        }
    }
}