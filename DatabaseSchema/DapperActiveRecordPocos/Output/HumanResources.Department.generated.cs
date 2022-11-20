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
using Dapper;

namespace MyNamespace
{
    [Table("Department", Schema = "HumanResources")]
    public partial class Department
    {
        #region Members
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short DepartmentId { get; set; }

        public string GroupName { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Name { get; set; }
        #endregion Members

        #region ActiveRecord
        public void Save()
        {
            if (DepartmentId == default(short))
                Insert();
            else
                Update();
        }
        public virtual void Insert()
        {
            using (var conn = IDbConnectionFactory.CreateConnection())
            {
                string cmd = @"
                    INSERT INTO [HumanResources].[Department]
                    (
                        [GroupName],
                        [ModifiedDate],
                        [Name]
                    )
                    VALUES
                    (
                        @GroupName,
                        @ModifiedDate,
                        @Name
                    )";
                this.DepartmentId = conn.Query<short>(cmd + "SELECT SCOPE_IDENTITY();", this).Single();
            }
        }
        public virtual void Update()
        {
            using (var conn = IDbConnectionFactory.CreateConnection())
            {
                string cmd = @"
                    UPDATE [HumanResources].[Department] SET
                        [GroupName] = @GroupName,
                        [ModifiedDate] = @ModifiedDate,
                        [Name] = @Name
                    WHERE
                        [DepartmentID] = @DepartmentId";
                conn.Execute(cmd, this);
            }
        }
        #endregion ActiveRecord

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
            Department other = obj as Department;
            if (other == null) return false;

            if (GroupName != other.GroupName)
                return false;
            if (ModifiedDate != other.ModifiedDate)
                return false;
            if (Name != other.Name)
                return false;
            return true;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (GroupName == null ? 0 : GroupName.GetHashCode());
                hash = hash * 23 + (ModifiedDate == default(DateTime) ? 0 : ModifiedDate.GetHashCode());
                hash = hash * 23 + (Name == null ? 0 : Name.GetHashCode());
                return hash;
            }
        }
        public static bool operator ==(Department left, Department right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Department left, Department right)
        {
            return !Equals(left, right);
        }
        #endregion Equals/GetHashCode
    }
}