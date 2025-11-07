using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using ParkingImporter.Data;
using ParkingApi.Endpoints;
using Jsonimporter.Api;
 
public class Program
{
    public static async Task Main(string[] args)
    {
        await AppInt.ImportJson();
    }
}
 
 