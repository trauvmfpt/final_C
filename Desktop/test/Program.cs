using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
/*using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;*/
using SpringHeroBank.entity;
using SpringHeroBank.model;
using SpringHeroBank.utility;
using SpringHeroBank.view;

namespace SpringHeroBank
{
    class Program
    {
        public static Account currentLoggedIn;

        static void Main(string[] args)
        {
            MainView.GenerateMenu();


        }


    }
}