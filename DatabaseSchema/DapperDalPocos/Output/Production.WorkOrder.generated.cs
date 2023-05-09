﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by dotnet-codegencs tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace MyNamespace
{
    [Table("WorkOrder", Schema = "Production")]
    public partial class WorkOrder
    {
        #region Members
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WorkOrderId { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        public int OrderQty { get; set; }

        public int ProductId { get; set; }

        public short ScrappedQty { get; set; }

        public short? ScrapReasonId { get; set; }

        public DateTime StartDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int StockedQty { get; set; }
        #endregion Members

        #region Equals/GetHashCode
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            WorkOrder other = obj as WorkOrder;
            if (other == null) return false;

            if (DueDate != other.DueDate)
                return false;
            if (EndDate != other.EndDate)
                return false;
            if (ModifiedDate != other.ModifiedDate)
                return false;
            if (OrderQty != other.OrderQty)
                return false;
            if (ProductId != other.ProductId)
                return false;
            if (ScrappedQty != other.ScrappedQty)
                return false;
            if (ScrapReasonId != other.ScrapReasonId)
                return false;
            if (StartDate != other.StartDate)
                return false;
            if (StockedQty != other.StockedQty)
                return false;
            return true;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (DueDate == default(DateTime) ? 0 : DueDate.GetHashCode());
                hash = hash * 23 + (EndDate == null ? 0 : EndDate.GetHashCode());
                hash = hash * 23 + (ModifiedDate == default(DateTime) ? 0 : ModifiedDate.GetHashCode());
                hash = hash * 23 + (OrderQty == default(int) ? 0 : OrderQty.GetHashCode());
                hash = hash * 23 + (ProductId == default(int) ? 0 : ProductId.GetHashCode());
                hash = hash * 23 + (ScrappedQty == default(short) ? 0 : ScrappedQty.GetHashCode());
                hash = hash * 23 + (ScrapReasonId == null ? 0 : ScrapReasonId.GetHashCode());
                hash = hash * 23 + (StartDate == default(DateTime) ? 0 : StartDate.GetHashCode());
                hash = hash * 23 + (StockedQty == default(int) ? 0 : StockedQty.GetHashCode());
                return hash;
            }
        }
        public static bool operator ==(WorkOrder left, WorkOrder right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WorkOrder left, WorkOrder right)
        {
            return !Equals(left, right);
        }
        #endregion Equals/GetHashCode
    }
}
