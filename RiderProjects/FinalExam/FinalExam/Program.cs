using System;
using System.Collections.Generic;
using System.Linq;

namespace FinalExam
{
    class Program
    {
        private static List<Product> listProducts = new List<Product>();
        
        public static void GenerateMenu()
        {
            while (true)
            {
                Console.WriteLine("---------WELCOME TO ADMIN MENU---------");
                Console.WriteLine("1. Add product records.");
                Console.WriteLine("2. Display product records.");
                Console.WriteLine("3. Delete product by Id.");
                Console.WriteLine("4. Exit.");
                Console.WriteLine("---------------------------------------------");
                Console.WriteLine("Please enter your choice (1|2|3|4): ");
                int choice = Convert.ToInt32(Console.ReadLine());
                switch (choice)
                {
                    case 1:
                        Add(listProducts);
                        break;
                    case 2:
                        Display(listProducts);
                        break;
                    case 3:
                        Delete(listProducts);
                        break;
                    case 4:
                        Console.WriteLine("See you later.");
                        Environment.Exit(1);
                        break;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }
        }
        
        public static void Add(List<Product> listProducts)
        {
            Console.WriteLine("Please enter product information");
            Console.WriteLine("-----------------------------------");
            Console.WriteLine("Product ID: ");
            var productId = Console.ReadLine();
            Console.WriteLine("Product Name: ");
            var name = Console.ReadLine();
            Console.WriteLine("Product Price (in number only): ");
            var price = Convert.ToDecimal(Console.ReadLine());
            var product = new Product(productId, name, price);
            try
            {
                listProducts.Add(product);
                Console.WriteLine("Added product successfully!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Can't add product.");
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        public static void Display(List<Product> listProducts)
        {
            Console.WriteLine(String.Format("{0,15}|{1,15}|{2,15}",
                "Product ID", "Product Name", "Price"));
            foreach (var product in listProducts)
            {
                Console.WriteLine(String.Format("{0,15}|{1,15}|{2,15}",
                    product.ProductId, product.Name, "$" + product.Price));
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        public static void Delete(List<Product> listProducts)
        {
            Console.WriteLine("Enter the Product's Id to delete: ");
            var productId = Console.ReadLine();
            if (!listProducts.Exists(x => x.ProductId == productId))
            {
                Console.WriteLine("No product with this Id!");
            }
            else
            {
                Console.WriteLine("Are you sure you want to delete product with Id = " + productId + "? (y|n)");
                var confirm = Console.ReadLine();
                if (confirm == "y")
                {
                    
                    var itemToRemove = listProducts.Single(r => r.ProductId == productId);
                    listProducts.Remove(itemToRemove);
                    Console.WriteLine("Successfully deleted product.");
                }

                Console.WriteLine("Didn't delete product.");
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }
        
        static void Main(string[] args)
        {
            listProducts.Add(new Product("MN001", "Monitor", 49));
            listProducts.Add(new Product("MN002", "Monitor 2", 45));
            listProducts.Add(new Product("MN003", "Monitor 3", 59));
            listProducts.Add(new Product("MN004", "Monitor 4", 099));
            GenerateMenu();
        }
    }
}