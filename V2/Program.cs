using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Import;
using Jsonimporter.Api;
public class Program
{
    public static void Main(string[] args)
    {
        
        AppInt.ImportJson().GetAwaiter().GetResult();
    }
}
