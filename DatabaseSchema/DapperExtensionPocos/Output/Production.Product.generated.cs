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
    [Table("Product", Schema = "Production")]
    public partial class Product
    {
        #region Members
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        public string Class { get; set; }
        public string Color { get; set; }
        public int DaysToManufacture { get; set; }
        public DateTime? DiscontinuedDate { get; set; }
        public bool FinishedGoodsFlag { get; set; }
        public decimal ListPrice { get; set; }
        public bool MakeFlag { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Name { get; set; }
        public string ProductLine { get; set; }
        public int? ProductModelId { get; set; }
        public string ProductNumber { get; set; }
        public int? ProductSubcategoryId { get; set; }
        public short ReorderPoint { get; set; }
        public Guid Rowguid { get; set; }
        public short SafetyStockLevel { get; set; }
        public DateTime? SellEndDate { get; set; }
        public DateTime SellStartDate { get; set; }
        public string Size { get; set; }
        public string SizeUnitMeasureCode { get; set; }
        public decimal StandardCost { get; set; }
        public string Style { get; set; }
        public decimal? Weight { get; set; }
        public string WeightUnitMeasureCode { get; set; }
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
            Product other = obj as Product;
            if (other == null) return false;

            if (Class != other.Class)
                return false;
            if (Color != other.Color)
                return false;
            if (DaysToManufacture != other.DaysToManufacture)
                return false;
            if (DiscontinuedDate != other.DiscontinuedDate)
                return false;
            if (FinishedGoodsFlag != other.FinishedGoodsFlag)
                return false;
            if (ListPrice != other.ListPrice)
                return false;
            if (MakeFlag != other.MakeFlag)
                return false;
            if (ModifiedDate != other.ModifiedDate)
                return false;
            if (Name != other.Name)
                return false;
            if (ProductLine != other.ProductLine)
                return false;
            if (ProductModelId != other.ProductModelId)
                return false;
            if (ProductNumber != other.ProductNumber)
                return false;
            if (ProductSubcategoryId != other.ProductSubcategoryId)
                return false;
            if (ReorderPoint != other.ReorderPoint)
                return false;
            if (Rowguid != other.Rowguid)
                return false;
            if (SafetyStockLevel != other.SafetyStockLevel)
                return false;
            if (SellEndDate != other.SellEndDate)
                return false;
            if (SellStartDate != other.SellStartDate)
                return false;
            if (Size != other.Size)
                return false;
            if (SizeUnitMeasureCode != other.SizeUnitMeasureCode)
                return false;
            if (StandardCost != other.StandardCost)
                return false;
            if (Style != other.Style)
                return false;
            if (Weight != other.Weight)
                return false;
            if (WeightUnitMeasureCode != other.WeightUnitMeasureCode)
                return false;
            return true;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Class == null ? 0 : Class.GetHashCode());
                hash = hash * 23 + (Color == null ? 0 : Color.GetHashCode());
                hash = hash * 23 + (DaysToManufacture == default(int) ? 0 : DaysToManufacture.GetHashCode());
                hash = hash * 23 + (DiscontinuedDate == null ? 0 : DiscontinuedDate.GetHashCode());
                hash = hash * 23 + (FinishedGoodsFlag == default(bool) ? 0 : FinishedGoodsFlag.GetHashCode());
                hash = hash * 23 + (ListPrice == default(decimal) ? 0 : ListPrice.GetHashCode());
                hash = hash * 23 + (MakeFlag == default(bool) ? 0 : MakeFlag.GetHashCode());
                hash = hash * 23 + (ModifiedDate == default(DateTime) ? 0 : ModifiedDate.GetHashCode());
                hash = hash * 23 + (Name == null ? 0 : Name.GetHashCode());
                hash = hash * 23 + (ProductLine == null ? 0 : ProductLine.GetHashCode());
                hash = hash * 23 + (ProductModelId == null ? 0 : ProductModelId.GetHashCode());
                hash = hash * 23 + (ProductNumber == null ? 0 : ProductNumber.GetHashCode());
                hash = hash * 23 + (ProductSubcategoryId == null ? 0 : ProductSubcategoryId.GetHashCode());
                hash = hash * 23 + (ReorderPoint == default(short) ? 0 : ReorderPoint.GetHashCode());
                hash = hash * 23 + (Rowguid == default(Guid) ? 0 : Rowguid.GetHashCode());
                hash = hash * 23 + (SafetyStockLevel == default(short) ? 0 : SafetyStockLevel.GetHashCode());
                hash = hash * 23 + (SellEndDate == null ? 0 : SellEndDate.GetHashCode());
                hash = hash * 23 + (SellStartDate == default(DateTime) ? 0 : SellStartDate.GetHashCode());
                hash = hash * 23 + (Size == null ? 0 : Size.GetHashCode());
                hash = hash * 23 + (SizeUnitMeasureCode == null ? 0 : SizeUnitMeasureCode.GetHashCode());
                hash = hash * 23 + (StandardCost == default(decimal) ? 0 : StandardCost.GetHashCode());
                hash = hash * 23 + (Style == null ? 0 : Style.GetHashCode());
                hash = hash * 23 + (Weight == null ? 0 : Weight.GetHashCode());
                hash = hash * 23 + (WeightUnitMeasureCode == null ? 0 : WeightUnitMeasureCode.GetHashCode());
                return hash;
            }
        }
        public static bool operator ==(Product left, Product right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Product left, Product right)
        {
            return !Equals(left, right);
        }
        #endregion Equals/GetHashCode
    }
}