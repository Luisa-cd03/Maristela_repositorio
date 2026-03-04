using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BellezaSuprema2.Models
{
    public class ReportesViewModel
    {
        public int TotalCitas { get; set; }
        public int CitasPendientes { get; set; }
        public int CitasCanceladas { get; set; }
        public int CitasFinalizadas { get; set; }
        public int TotalUsuarios { get; set; }
        public int IngresosEstimados { get; set; }
    }
}
