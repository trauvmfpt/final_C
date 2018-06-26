namespace FinalExam
{
    public class Product
    {
        private string productId;
        private string name;
        private decimal price;

        public Product(string productId, string name, decimal price)
        {
            this.productId = productId;
            this.name = name;
            this.price = price;
        }

        public string ProductId
        {
            get => productId;
            set => productId = value;
        }

        public string Name
        {
            get => name;
            set => name = value;
        }

        public decimal Price
        {
            get => price;
            set => price = value;
        }
    }
}