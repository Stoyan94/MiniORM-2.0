﻿using Microsoft.Data.SqlClient;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using static MiniORM.ErrorMessages;

namespace MiniORM
{
    public class DbContext
    {
        private readonly DatabaseConnection dbConnection;
        private readonly IDictionary<Type, PropertyInfo> dbSetProperties;

        protected DbContext(string connectionString)
        {
            this.dbConnection = new DatabaseConnection(connectionString);
            this.dbSetProperties = this.DiscoverDbSet();
            using (new ConnectionManager(this.dbConnection))
            {
                this.InitializeDbSets();
            }
            this.MapAllRelations(); // This is done after connection close because it is in-memory operation
        }

        internal static readonly Type[] AllowedSqlTypes =
        {
            typeof(string),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(DateTime),
            typeof(Guid)
        };

        public void SaveChanges()
        {
            IEnumerable<object> dbSetsObjects = this.dbSetProperties
                .Select(edb => edb.Value.GetValue(this)!)
                .ToArray();

            foreach (IEnumerable<object> dbSet in dbSetsObjects)
            {
                IEnumerable<object> invalidEntities = dbSet
                    .Where(e => !IsObjectValid(e))
                    .ToArray();

                if (invalidEntities.Any())
                {
                    throw new InvalidOperationException(string.Format(InvalidEntitiesInDbSetMessage,
                        invalidEntities.Count(),dbSet.GetType().Name));
                }
            }

            using (new ConnectionManager(this.dbConnection))
            {
                using SqlTransaction transaction = this.dbConnection
                    .StartTransaction();

                foreach (IEnumerable dbSet in dbSetsObjects)
                {
                    MethodInfo persistMethod = typeof(DbContext)
                        .GetMethod("Persist", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(dbSet.GetType());

                    try
                    {
                        try
                        {
                            persistMethod.Invoke(this, new object[] { dbSet });
                        }
                        catch (TargetInvocationException tie)
                            when (tie.InnerException != null)
                        {

                            throw tie.InnerException;
                        }
                    }
                    catch 
                    {
                        Console.WriteLine(TransactionRollbackMessage);
                        transaction.Rollback();
                        throw;
                    }

                    try
                    {
                        transaction.Commit();
                    }
                    catch
                    {
                        Console.WriteLine(TransactionExceptionMessage);
                        throw;
                    }                    
                   
                }
            }
        }

        private static bool IsObjectValid(object obj)
        {
            ValidationContext validationContext = new ValidationContext(obj);
            ICollection<ValidationResult> validationErros = new List<ValidationResult>();

            return Validator
                .TryValidateObject(obj, validationContext, validationErros, true);
        }       

        private IDictionary<Type, PropertyInfo> DiscoverDbSet()
        {
            return this.GetType()
                .GetProperties()
                .Where(pi => pi.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .ToDictionary(pi => pi.PropertyType.GetGenericArguments().First(), pi => pi);
        }

        private void InitializeDbSets()
        {
            foreach (KeyValuePair<Type, PropertyInfo> dbSetKvp in dbSetProperties)
            {
                Type dbSetType = dbSetKvp.Key;
                PropertyInfo dbSetPoperty = dbSetKvp.Value;

                MethodInfo populateDbSetMethodInfo = typeof(DbContext)
                    .GetMethod("PopulateDbSet", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(dbSetType);

                populateDbSetMethodInfo.Invoke(this, new object[] { dbSetPoperty });
            }
        }

        private void MapAllRelations()
        {
            foreach (KeyValuePair<Type, PropertyInfo> dbSetKvp in dbSetProperties)
            {
                Type dbSetType = dbSetKvp.Key;
                PropertyInfo dbSetPropertyInfo = dbSetKvp.Value;

                MethodInfo mapRelationsGenericMethodInfo = typeof(DbContext)
                    .GetMethod("MapRelations", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(dbSetType);

                object? dbSetInstance = dbSetPropertyInfo.GetValue(this);

                if (dbSetInstance == null)
                {
                    throw new ArgumentNullException(dbSetPropertyInfo.Name,
                        String.Format(NullDbSetMessage, dbSetPropertyInfo.Name));
                }

                mapRelationsGenericMethodInfo.Invoke(this, new object[] { dbSetInstance });
            }
        }

        private void Persist<TEntity> (DbSet<TEntity> dbSet)
            where TEntity : class, new()
        {
            string tableName = this.GetTableName(typeof(TEntity));
            IEnumerable<string> columnsNames = this.dbConnection
                .FetchColumnNames(tableName);

            if (dbSet.ChangeTracker.AddedEntities.Any())
            {
                this.dbConnection.InsertEntities(dbSet.ChangeTracker.AddedEntities, tableName, columnsNames.ToArray());
            }

            IEnumerable<TEntity> modifiedEntities = dbSet
                .ChangeTracker.GetModifiedEntities(dbSet);

            if (modifiedEntities.Any())
            {
                this.dbConnection
                    .UpdateEntities(modifiedEntities, tableName, columnsNames.ToArray());
            }

            if (dbSet.ChangeTracker.RemovedEntities.Any())
            {
                this.dbConnection
                    .DeleteEntities(dbSet.ChangeTracker.RemovedEntities, tableName, columnsNames.ToArray());
            }
        }

        private void PopulateDbSet<TEntity>(PropertyInfo dbSetPropertyInfo)
            where TEntity : class, new()
        {
            IEnumerable<TEntity> dbSetEntities = this.LoadTableEntities<TEntity>();
            DbSet<TEntity> dbSetInstance = new DbSet<TEntity>(dbSetEntities);
            ReflectionHelper.ReplaceBackingField(this, dbSetPropertyInfo.Name, dbSetInstance);
        }


        private IEnumerable<TEntity> LoadTableEntities<TEntity>()
          where TEntity : class
        {
            Type tableType = typeof(TEntity);
            IEnumerable<string> columnNames = this.GetEntityColumnNames(tableType);
            string tableName = this.GetTableName(tableType);

            return this.dbConnection
                .FetchResultSet<TEntity>(tableName, columnNames.ToArray());
        }

        
        private IEnumerable<string> GetEntityColumnNames(Type entityType)
        {
            string tableName = this.GetTableName(entityType);
            IEnumerable<string> tableColumnNames = this.dbConnection
                .FetchColumnNames(tableName);

            IEnumerable<string> entityColumnNames = entityType
                .GetProperties()
                .Where(pi => tableColumnNames.Contains(pi.Name) &&
                             !pi.HasAttribute<NotMappedAttribute>() &&
                             AllowedSqlTypes.Contains(pi.PropertyType))
                .Select(pi => pi.Name)
                .ToArray();

            return entityColumnNames;
        }

        private string GetTableName(Type tableType)
        {
            Attribute? tableNameAtr = Attribute.GetCustomAttribute(tableType, typeof(TableAttribute));

            if (tableNameAtr == null)
            {
                return this.dbSetProperties[tableType].Name;
            }

            if (tableNameAtr is TableAttribute tableNameAttrConf)
            {
                return tableNameAttrConf.Name;
            }

            throw new ArgumentException(String.Format(NoTableNameFound, this.dbSetProperties[tableType].Name));
        }

        private void MapRelations<TEntity>(DbSet<TEntity> dbSet)
            where TEntity : class, new()
        {
            Type entityType = typeof(TEntity);

            this.MapNavigationProperties(dbSet);

            IEnumerable<PropertyInfo> entityCollections = entityType
                .GetProperties()
                .Where(pi => pi.PropertyType.IsGenericType &&
                             pi.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>));

            foreach (PropertyInfo entityCollectionPropInfo in entityCollections)
            {
                Type collectionEntityType = entityCollectionPropInfo
                    .PropertyType
                    .GenericTypeArguments
                    .First();

                MethodInfo mapCollectionGenMethodInfo = typeof(DbContext)
                    .GetMethod("MapCollection", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType, collectionEntityType);

                mapCollectionGenMethodInfo.Invoke(this, new object[] { dbSet, entityCollectionPropInfo });
            }
        }


        private void MapNavigationProperties<TEntiy>(DbSet<TEntiy> dbSet)
            where TEntiy : class, new()
        {
            Type entityType = typeof(TEntiy);

            IEnumerable<PropertyInfo> foreignKeys = entityType
                .GetProperties()
                .Where(pi => pi.HasAttribute<ForeignKeyAttribute>());

            foreach (PropertyInfo fkPropertyInfo in foreignKeys)
            {
                string navigationPropName = fkPropertyInfo
                    .GetCustomAttribute<ForeignKeyAttribute>()!.Name;

                PropertyInfo? navigationPropertyInfo = entityType
                    .GetProperty(navigationPropName);

                if (navigationPropertyInfo == null)
                {
                    throw new ArgumentException(String.Format(InvalidNavigationPropertyName,
                        fkPropertyInfo.Name, navigationPropName));
                }

                object? navDbSetInstance = 
                    this.dbSetProperties[navigationPropertyInfo.PropertyType].GetValue(this);

                if (navDbSetInstance == null)
                {
                    throw new ArgumentException(String.Format(NavPropertyWithoutDbSetMessage,
                        navigationPropName, navigationPropertyInfo.PropertyType));
                }

                PropertyInfo navEntityPkPropInfo = navigationPropertyInfo
                 .PropertyType
                 .GetProperties()
                 .First(pi => pi.HasAttribute<KeyAttribute>());
                foreach (TEntiy entity in dbSet)
                {
                    object? fkValue = fkPropertyInfo.GetValue(entity);
                    if (fkValue == null)
                    {
                        navigationPropertyInfo.SetValue(entity, null);
                        continue;
                    }

                    object? navPropValueEntity = ((IEnumerable<object>)navDbSetInstance)
                        .First(currNavPropEntity => navEntityPkPropInfo
                            .GetValue(currNavPropEntity)!
                            .Equals(fkValue));
                    navigationPropertyInfo.SetValue(entity, navPropValueEntity);
                }

            }
        }


        private void MapCollection<TDbSet, TCollection>(DbSet<TDbSet> dbSet, PropertyInfo collectionPropInfo)
           where TDbSet : class, new()
           where TCollection : class, new()
        {
            Type entityType = typeof(TDbSet);
            Type collectionType = typeof(TCollection);

            IEnumerable<PropertyInfo> collectionPrimaryKeys = collectionType
                .GetProperties()
                .Where(pi => pi.HasAttribute<KeyAttribute>());

            PropertyInfo foreignKey = collectionType
                .GetProperties()
                .First(pi => pi.HasAttribute<ForeignKeyAttribute>() &&
                                        collectionType
                                             .GetProperty(pi.GetCustomAttribute<ForeignKeyAttribute>()!.Name)!
                                             .PropertyType == entityType);
            PropertyInfo primaryKey = entityType
                .GetProperties()
                .First(pi => pi.HasAttribute<KeyAttribute>());


            DbSet<TCollection> navDbSet = (DbSet<TCollection>)
                this.dbSetProperties[collectionType]
                    .GetValue(this)!;
            foreach (TDbSet dbSetEntity in dbSet)
            {
                object pkValue = primaryKey.GetValue(dbSetEntity)!;
                IEnumerable<TCollection> navCollectionEntities = navDbSet
                    .Where(navEntity => foreignKey.GetValue(navEntity) != null &&
                                                  foreignKey.GetValue(navEntity)!.Equals(pkValue))
                    .ToArray();
                ReflectionHelper.ReplaceBackingField(dbSetEntity, collectionPropInfo.Name, navCollectionEntities);
            }
        }

    }
}
