using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Microsoft.WindowsAzure.MobileServices.Sync;

namespace DevDaysTasks
{
    public partial class TodoItemManager
    {
        MobileServiceClient client;
        IMobileServiceSyncTable<TodoItem> todoTable;
        // istanza Singleton
        private static TodoItemManager defaultManager;
        public static TodoItemManager DefaultManager
        {
            get { return defaultManager ?? (defaultManager = new TodoItemManager()); }
        }

        public TodoItemManager()
        {
            defaultManager = this;
        }

        public async Task Initialize()
        {
            // verificare se il contesto di sincronizzazione è già stato sincronizzato
            if (client?.SyncContext?.IsInitialized ?? false)
                return;
            // inizializza client per dispositivi mobili
            client = new MobileServiceClient(Constants.ApplicationURL);
            // inizializzare il DB locale per la sincronizzazione
            var path = Path.Combine(MobileServiceClient.DefaultDatabasePath, Constants.SyncStorePath);
            var store = new MobileServiceSQLiteStore(path);
            store.DefineTable<TodoItem>();
            // iizializza SyncContext utilizzando l'oggetto predefinito IMobileServiceSyncHandler
            var handler = new MobileServiceSyncHandler();
            await client.SyncContext.InitializeAsync(store, handler);
            todoTable = client.GetSyncTable<TodoItem>();
        }

        public async Task<ObservableCollection<TodoItem>> GetTodoItemsAsync(bool syncItems = false)
        {
            try
            {
                // assicurarsi che il gestore sia stato inizializzato
                await Initialize();
                // verificare se è richiesta la sincronizzazione con il back-end
                if (syncItems)
                {
                    await SyncAsync();
                }
                // ottenere tutti gli elementi non completati dal DB locale
                var items = await todoTable
                    .Where(todoItem => !todoItem.Done)
                    .OrderBy(todoItem => todoItem.Name)
                    .ToEnumerableAsync();
                return new ObservableCollection<TodoItem>(items);
            }
            catch (MobileServiceInvalidOperationException msioe)
            {
                Debug.WriteLine(@"Operazione di sincronizzazione non valida: {0}", msioe.Message);
                throw;
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"Errore di sincronizzazione: {0}", e.Message);
                throw;
            }
        }

        public async Task SaveTaskAsync(TodoItem item)
        {
            // assicurarsi che il gestore sia stato inizializzato
            await Initialize();
            // controllare se l'elemento è nuovo o è già esistente controllando il relativo ID
            if (item.Id == null)
                // inserisci nuovo elemento
                await todoTable.InsertAsync(item);
            else
                // aggiorna elemento esistente
                await todoTable.UpdateAsync(item);
        }

        public async Task SyncAsync()
        {
            ReadOnlyCollection<MobileServiceTableOperationError> syncErrors = null;
            // assicurarsi che il gestore sia stato inizializzato
            await Initialize();
            try
            {
                await client.SyncContext.PushAsync();
                /* i primo parametro è un nome di query utilizzato internamente dall'SDK
                 * client per implementare la sincronizzazione incrementale, uusare
                 * un nome di query diverso per ogni query univoca nell'app  */
                await todoTable.PullAsync("allTodoItems", todoTable.CreateQuery());
            }
            catch (MobileServicePushFailedException exc)
            {
                if (exc.PushResult != null)
                    syncErrors = exc.PushResult.Errors;
            }
            /* gestione di errori/conflitti, un'app reale gestirebbe i vari errori come le
             * condizioni di rete, conflitti tra server e altri tramite IMobileServiceSyncHandler  */
            if (syncErrors != null)
            {
                foreach (var error in syncErrors)
                {
                    if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Result != null)
                        // aggiornamento non riuscito, ripristino della copia del server
                        await error.CancelAndUpdateItemAsync(error.Result);
                    else if (error.OperationKind != MobileServiceTableOperationKind.Update)
                        // eliminare le modifiche locali
                        await error.CancelAndDiscardItemAsync();
                    Debug.WriteLine(@"Errore durante l'esecuzione dell'operazione di sincronizzazione. Item: {0} ({1}). Operazione scartata.", error.TableName, error.Item["id"]);
                }
            }
        }
    }
}