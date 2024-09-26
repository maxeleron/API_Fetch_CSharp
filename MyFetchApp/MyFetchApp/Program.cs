using System;
using System.Data.OleDb;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    // З'єднання з базою MS Access
    static string connectionString = @"Provider = Microsoft.Jet.OLEDB.4.0; Data Source=mainDb.mdb";
    // Сигнатури API для обробки подій Windows
    [DllImport("Kernel32")]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

    private delegate bool HandlerRoutine(CtrlTypes CtrlType);

    private enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.Default;
        Console.Title = "MyFetchApp";
        // Додавання обробника закриття вікна через Windows UI
        SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);

        while (true)
        {
            Console.Write("Enter name or command (displaydb/cleardb/exit): ");
            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input)) continue;

            switch (input.ToLower())
            {
                case "displaydb":
                    DisplayDatabase();
                    break;
                case "cleardb":
                    ClearDatabase();
                    break;
                case "exit":
                    ClearDatabase();
                    Thread.Sleep(1000);
                    Environment.Exit(0);

                    break;
                default:
                    FetchAndSaveData(input).Wait();
                    break;
            }

        }
    }
    // Обробник подій Windows
    private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
    {
        switch (ctrlType)
        {
            case CtrlTypes.CTRL_CLOSE_EVENT:
                ClearDatabase();  // Очищення бази даних при закритті через хрестик
                Console.ReadKey();
                return true;
            default:
                return false;
        }
    }

    // Запит до API і збереження результатів
    static async Task FetchAndSaveData(string name)
    {
        string apiUrl = $"https://api.nationalize.io/?name={name}";

        using (HttpClient client = new HttpClient())
        {
            try
            {
                string response = await client.GetStringAsync(apiUrl);
                var result = JsonConvert.DeserializeObject<NationalizeResponse>(response);
                Console.WriteLine($"Name: {result.name} | Matches: {result.count}");

                foreach (var country in result.country)
                {
                    Console.WriteLine($"Country: {country.country_id}, Probability: {country.probability}");
                    SaveToDatabase(result.name, country.country_id, country.probability);
                }
                Console.WriteLine("Request recorded to DB");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"Request error: {ex.Message}");
            }
        }
    }

    // Збереження даних в базу Access
    static void SaveToDatabase(string name, string country, double probability)
    {
        using (OleDbConnection connection = new OleDbConnection(connectionString))
        {
            connection.Open();
            string query = "INSERT INTO PeopleData (personName, Country, Probability) VALUES (@personName, @Country, @Probability)";
            using (OleDbCommand command = new OleDbCommand(query, connection))
            {
                command.Parameters.AddWithValue("@personName", name);
                command.Parameters.AddWithValue("@Country", country);
                command.Parameters.AddWithValue("@Probability", probability);
                command.ExecuteNonQuery();
            }
        }
    }

    // Виведення даних з бази
    static void DisplayDatabase()
    {
        using (OleDbConnection connection = new OleDbConnection(connectionString))
        {
            connection.Open();

            // Отримати кількість записів у таблиці
            string countQuery = "SELECT COUNT(*) FROM PeopleData";
            using (OleDbCommand countCommand = new OleDbCommand(countQuery, connection))
            {
                int recordCount = (int)countCommand.ExecuteScalar();
                Console.WriteLine($"DB records count: ({recordCount})");
            }

            // Отримати та відобразити всі записи з таблиці
            string query = "SELECT * FROM PeopleData";
            using (OleDbCommand command = new OleDbCommand(query, connection))
            {
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"ID: {reader["Id"]} | Name: {reader["personName"]} | Country: {reader["Country"]} | Probability: {reader["Probability"]}");
                    }
                }
            }
            Console.WriteLine();
        }
    }


    // Очищення бази
    static void ClearDatabase()
    {
        using (OleDbConnection connection = new OleDbConnection(connectionString))
        {
            connection.Open();
            string query = "DELETE FROM PeopleData";
            using (OleDbCommand command = new OleDbCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
            Console.WriteLine("DB cleared.");
            Console.WriteLine();
        }
    }


}


// Модель для десеріалізації відповіді API
public class NationalizeResponse
{
    public string count { get; set; }
    public string name { get; set; }
    public Country[] country { get; set; }
}

public class Country
{
    public string country_id { get; set; }
    public double probability { get; set; }
}
