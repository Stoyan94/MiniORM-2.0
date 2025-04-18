﻿namespace MiniORM
{
    public static class ErrorMessages
    {
        public static string PrimaryKeyNullErrorMessage =
              @"The primary keys cannot have null values!";

        public static string InvalidEntitiesInDbSetMessage =
            @"{0} Invalid Entities Found in {1}!";

        public static string NoTableNameFound =
            @"Could not find a valid table name for DbSet {0}!";

        public static string NullDbSetMessage =
            @"Fatal error occurred! The DbSet {0} is null!";

        public static string InvalidNavigationPropertyName =
            @"Foreign key {0} references navigation property with name {1} which does not exist!";

        public static string NavPropertyWithoutDbSetMessage =
            @"DbSet could not be found for navigation property {0} of type {1}!";

        public static string TransactionRollbackMessage =
            @"Performing Rollback due to Exception!!!";

        public static string TransactionExceptionMessage =
            @"The SQL Transaction failed due to unexpected error!";
    }
}
