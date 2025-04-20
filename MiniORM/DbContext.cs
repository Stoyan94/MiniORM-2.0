using Microsoft.Data.SqlClient;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using static MiniORM.ErrorMessages;

namespace MiniORM
{
    public class DbContext // Основен клас за взаимодействие с базата данни
    {
        private readonly DatabaseConnection dbConnection; // Инстанция на клас за връзка с база данни
        private readonly IDictionary<Type, PropertyInfo> dbSetProperties; // Колекция от свойства, които представляват DbSet-ове на различни типове

        // Конструктор, който инициализира връзката с базата данни и всички DbSet свойства
        protected DbContext(string connectionString)
        {
            this.dbConnection = new DatabaseConnection(connectionString); // Създава връзка към базата данни с подаденото connectionString
            this.dbSetProperties = this.DiscoverDbSet(); // Открива всички свойства от тип DbSet
            using (new ConnectionManager(this.dbConnection)) // Управлява връзката с базата данни (предпазва от грешки при използване)
            {
                this.InitializeDbSets(); // Инициализира DbSet-овете
            }
            this.MapAllRelations(); // След като връзката е затворена, прави всички мапвания на релации между обектите в паметта
        }

        // Позволени SQL типове за съхранение в базата данни
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

        // Метод за записване на промените в базата данни
        public void SaveChanges()
        {
            // Вземаме всички DbSet обекти
            IEnumerable<object> dbSetsObjects = this.dbSetProperties
                .Select(edb => edb.Value.GetValue(this)!) // Вземаме стойността на всяко свойство
                .ToArray();

            // Проверяваме дали има невалидни обекти
            foreach (IEnumerable<object> dbSet in dbSetsObjects)
            {
                IEnumerable<object> invalidEntities = dbSet
                    .Where(e => !IsObjectValid(e)) // Проверяваме валидността на обектите
                    .ToArray();

                // Ако има невалидни обекти, хвърляме грешка
                if (invalidEntities.Any())
                {
                    throw new InvalidOperationException(string.Format(InvalidEntitiesInDbSetMessage,
                        invalidEntities.Count(), dbSet.GetType().Name));
                }
            }

            // Започваме транзакция с връзката към базата данни
            using (new ConnectionManager(this.dbConnection))
            {
                using SqlTransaction transaction = this.dbConnection
                    .StartTransaction(); // Старт на транзакцията

                // Обхождаме всички DbSet обекти и ги записваме в базата данни
                foreach (IEnumerable dbSet in dbSetsObjects)
                {
                    MethodInfo persistMethod = typeof(DbContext)
                        .GetMethod("Persist", BindingFlags.NonPublic | BindingFlags.Instance)! // Намираме метода Persist
                        .MakeGenericMethod(dbSet.GetType()); // Генерираме метода за конкретния тип DbSet

                    try
                    {
                        try
                        {
                            persistMethod.Invoke(this, new object[] { dbSet }); // Извикваме метода Persist за запис на обектите
                        }
                        catch (TargetInvocationException tie)
                            when (tie.InnerException != null)
                        {
                            throw tie.InnerException; // Хващаме вътрешната изключение, ако има
                        }
                    }
                    catch
                    {
                        Console.WriteLine(TransactionRollbackMessage); // Печатаме съобщение за неуспешна транзакция
                        transaction.Rollback(); // Отменяме транзакцията
                        throw;
                    }

                    try
                    {
                        transaction.Commit(); // Потвърждаваме транзакцията
                    }
                    catch
                    {
                        Console.WriteLine(TransactionExceptionMessage); // Печатаме съобщение за грешка при потвърждаване
                        throw;
                    }
                }
            }
        }

        // Метод за валидиране на обект с помощта на DataAnnotations
        private static bool IsObjectValid(object obj)
        {
            ValidationContext validationContext = new ValidationContext(obj); // Създаване на контекст за валидиране
            ICollection<ValidationResult> validationErros = new List<ValidationResult>(); // Списък с грешки, ако има такива

            return Validator
                .TryValidateObject(obj, validationContext, validationErros, true); // Опитваме се да валидираме обекта
        }

        // Метод за откриване на всички DbSet свойства в контекста
        private IDictionary<Type, PropertyInfo> DiscoverDbSet()
        {
            return this.GetType()
                .GetProperties() // Вземаме всички свойства на типа
                .Where(pi => pi.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)) // Филтрираме само DbSet свойства
                .ToDictionary(pi => pi.PropertyType.GetGenericArguments().First(), pi => pi); // Връщаме като речник с типове като ключове
        }

        // Метод за инициализиране на всички DbSet
        private void InitializeDbSets()
        {
            // Обхождаме всички DbSet свойства
            foreach (KeyValuePair<Type, PropertyInfo> dbSetKvp in dbSetProperties)
            {
                Type dbSetType = dbSetKvp.Key; // Тип на DbSet
                PropertyInfo dbSetPoperty = dbSetKvp.Value; // Свойство за DbSet

                // Намираме метода PopulateDbSet за конкретния тип DbSet
                MethodInfo populateDbSetMethodInfo = typeof(DbContext)
                    .GetMethod("PopulateDbSet", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(dbSetType);

                populateDbSetMethodInfo.Invoke(this, new object[] { dbSetPoperty }); // Извикваме метода за инициализиране на DbSet
            }
        }

        // Метод за мапване на всички релации в базата данни
        private void MapAllRelations()
        {
            // Обхождаме всички DbSet свойства
            foreach (KeyValuePair<Type, PropertyInfo> dbSetKvp in dbSetProperties)
            {
                Type dbSetType = dbSetKvp.Key; // Тип на DbSet
                PropertyInfo dbSetPropertyInfo = dbSetKvp.Value; // Свойство за DbSet

                // Намираме метода MapRelations за конкретния тип DbSet
                MethodInfo mapRelationsGenericMethodInfo = typeof(DbContext)
                    .GetMethod("MapRelations", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(dbSetType);

                object? dbSetInstance = dbSetPropertyInfo.GetValue(this); // Вземаме инстанцията на DbSet

                if (dbSetInstance == null)
                {
                    throw new ArgumentNullException(dbSetPropertyInfo.Name,
                        String.Format(NullDbSetMessage, dbSetPropertyInfo.Name)); // Хвърляме грешка ако DbSet е null
                }

                mapRelationsGenericMethodInfo.Invoke(this, new object[] { dbSetInstance }); // Извикваме метода за мапване на релации
            }
        }

        // Метод за запазване на промените за конкретен тип DbSet
        private void Persist<TEntity>(DbSet<TEntity> dbSet)
            where TEntity : class, new()
        {
            string tableName = this.GetTableName(typeof(TEntity)); // Вземаме името на таблицата за този тип
            IEnumerable<string> columnsNames = this.dbConnection
                .FetchColumnNames(tableName); // Вземаме имена на колоните от таблицата

            // Ако има нови обекти, ги записваме в базата данни
            if (dbSet.ChangeTracker.AddedEntities.Any())
            {
                this.dbConnection.InsertEntities(dbSet.ChangeTracker.AddedEntities, tableName, columnsNames.ToArray());
            }

            // Ако има променени обекти, ги обновяваме в базата
            IEnumerable<TEntity> modifiedEntities = dbSet
                .ChangeTracker.GetModifiedEntities(dbSet);
            if (modifiedEntities.Any())
            {
                this.dbConnection
                    .UpdateEntities(modifiedEntities, tableName, columnsNames.ToArray());
            }

            // Ако има изтрити обекти, ги изтриваме от базата
            if (dbSet.ChangeTracker.RemovedEntities.Any())
            {
                this.dbConnection
                    .DeleteEntities(dbSet.ChangeTracker.RemovedEntities, tableName, columnsNames.ToArray());
            }
        }

        // Метод за попълване на DbSet с данни от таблица
        private void PopulateDbSet<TEntity>(PropertyInfo dbSetPropertyInfo)
            where TEntity : class, new()
        {
            IEnumerable<TEntity> dbSetEntities = this.LoadTableEntities<TEntity>(); // Зареждаме всички записи за този тип
            DbSet<TEntity> dbSetInstance = new DbSet<TEntity>(dbSetEntities); // Създаваме инстанция на DbSet
            ReflectionHelper.ReplaceBackingField(this, dbSetPropertyInfo.Name, dbSetInstance); // Заместваме стойността на свойството
        }

        // Метод за зареждане на всички записи от таблица за конкретен тип
        private IEnumerable<TEntity> LoadTableEntities<TEntity>()
          where TEntity : class
        {
            Type tableType = typeof(TEntity); // Типа на таблицата
            IEnumerable<string> columnNames = this.GetEntityColumnNames(tableType); // Вземаме имената на колоните
            string tableName = this.GetTableName(tableType); // Вземаме името на таблицата

            return this.dbConnection
                .FetchResultSet<TEntity>(tableName, columnNames.ToArray()); // Зареждаме всички записи от таблицата
        }

        // Метод за вземане на имената на колоните на таблицата за конкретен тип
        private IEnumerable<string> GetEntityColumnNames(Type entityType)
        {
            string tableName = this.GetTableName(entityType); // Вземаме името на таблицата
            IEnumerable<string> tableColumnNames = this.dbConnection
                .FetchColumnNames(tableName); // Вземаме имената на колоните в таблицата

            IEnumerable<string> entityColumnNames = entityType
                .GetProperties() // Вземаме всички свойства на типа
                .Where(pi => tableColumnNames.Contains(pi.Name) && // Ако името на свойството съвпада с името на колона
                             !pi.HasAttribute<NotMappedAttribute>() && // Ако не е маркирано с NotMappedAttribute
                             AllowedSqlTypes.Contains(pi.PropertyType)) // Ако типът е позволен в базата
                .Select(pi => pi.Name) // Вземаме само името на свойството
                .ToArray();

            return entityColumnNames; // Връщаме имената на колоните
        }

        // Метод за вземане на името на таблицата за конкретен тип
        private string GetTableName(Type tableType)
        {
            Attribute? tableNameAtr = Attribute.GetCustomAttribute(tableType, typeof(TableAttribute)); // Проверяваме дали има атрибут Table
            if (tableNameAtr == null)
            {
                return this.dbSetProperties[tableType].Name; // Ако няма атрибут, вземаме името от свойството
            }

            if (tableNameAtr is TableAttribute tableNameAttrConf)
            {
                return tableNameAttrConf.Name; // Ако има атрибут, вземаме името на таблицата от него
            }

            throw new ArgumentException(String.Format(NoTableNameFound, this.dbSetProperties[tableType].Name)); // Ако няма атрибут с името на таблицата
        }

        // Метод за мапване на релации между обектите в паметта (например one-to-many)
        // В тази част ще се мапват релации като one-to-many или други между обектите в паметта


        private void MapRelations<TEntity>(DbSet<TEntity> dbSet)
                where TEntity : class, new()
        {
            // Получава типа на текущия ентитет (клас), с който работим.
            Type entityType = typeof(TEntity);

            // Извиква метода MapNavigationProperties, който се грижи за навигационните свойства.
            this.MapNavigationProperties(dbSet);

            // Получаваме всички колекции, които са дефинирани като ICollection<> (напр. List<OtherEntity>)
            IEnumerable<PropertyInfo> entityCollections = entityType
                .GetProperties() // Вземаме всички свойства на ентитета.
                .Where(pi => pi.PropertyType.IsGenericType && // Проверяваме дали е генеричен тип.
                             pi.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)); // Проверяваме дали е ICollection<T>

            // За всяко свойство, което е колекция от ентитети, извикваме метод, който обработва съответната колекция.
            foreach (PropertyInfo entityCollectionPropInfo in entityCollections)
            {
                // Получаваме типа на елементите в колекцията (напр. за ICollection<Order> ще е Order).
                Type collectionEntityType = entityCollectionPropInfo
                    .PropertyType
                    .GenericTypeArguments
                    .First();

                // Вземаме метода "MapCollection", но го правим универсален за конкретните типове.
                MethodInfo mapCollectionGenMethodInfo = typeof(DbContext)
                    .GetMethod("MapCollection", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType, collectionEntityType);

                // Извикваме метода "MapCollection" за мапване на колекцията за всеки ентитет.
                mapCollectionGenMethodInfo.Invoke(this, new object[] { dbSet, entityCollectionPropInfo });
            }
        }

        private void MapNavigationProperties<TEntiy>(DbSet<TEntiy> dbSet)
                where TEntiy : class, new()
        {
            // Получава типа на текущия ентитет.
            Type entityType = typeof(TEntiy);

            // Извлича всички свойства, които имат атрибут ForeignKeyAttribute (свързани с външни ключове).
            IEnumerable<PropertyInfo> foreignKeys = entityType
                .GetProperties() // Вземаме всички свойства на ентитета.
                .Where(pi => pi.HasAttribute<ForeignKeyAttribute>()); // Филтрираме по атрибут ForeignKeyAttribute.

            // За всяко външно ключово свойство...
            foreach (PropertyInfo fkPropertyInfo in foreignKeys)
            {
                // Извличаме името на навигационното свойство (съответстващото свойство, което е свързано с FK).
                string navigationPropName = fkPropertyInfo
                    .GetCustomAttribute<ForeignKeyAttribute>()!.Name;

                // Опитваме да намерим навигационното свойство в класа.
                PropertyInfo? navigationPropertyInfo = entityType
                    .GetProperty(navigationPropName);

                // Ако не е намерено навигационното свойство, хвърляме грешка.
                if (navigationPropertyInfo == null)
                {
                    throw new ArgumentException(String.Format(InvalidNavigationPropertyName,
                        fkPropertyInfo.Name, navigationPropName));
                }

                // Вземаме DbSet за навигационния ентитет.
                object? navDbSetInstance =
                    this.dbSetProperties[navigationPropertyInfo.PropertyType].GetValue(this);

                // Ако не е намерен DbSet за навигационния ентитет, хвърляме грешка.
                if (navDbSetInstance == null)
                {
                    throw new ArgumentException(String.Format(NavPropertyWithoutDbSetMessage,
                        navigationPropName, navigationPropertyInfo.PropertyType));
                }

                // Вземаме PropertyInfo за Primary Key на навигационния ентитет.
                PropertyInfo navEntityPkPropInfo = navigationPropertyInfo
                 .PropertyType
                 .GetProperties()
                 .First(pi => pi.HasAttribute<KeyAttribute>());

                // За всеки ентитет в DbSet-а на текущия ентитет...
                foreach (TEntiy entity in dbSet)
                {
                    // Вземаме стойността на външния ключ (FK).
                    object? fkValue = fkPropertyInfo.GetValue(entity);

                    // Ако FK е null, задаваме стойността на навигационното свойство на null и преминаваме към следващия ентитет.
                    if (fkValue == null)
                    {
                        navigationPropertyInfo.SetValue(entity, null);
                        continue;
                    }

                    // Ако FK има стойност, търсим съответния ентитет в навигационната колекция, базирано на Primary Key.
                    object? navPropValueEntity = ((IEnumerable<object>)navDbSetInstance)
                        .First(currNavPropEntity => navEntityPkPropInfo
                            .GetValue(currNavPropEntity)!
                            .Equals(fkValue));

                    // Задаваме навигационното свойство на текущия ентитет.
                    navigationPropertyInfo.SetValue(entity, navPropValueEntity);
                }
            }
        }


        private void MapCollection<TDbSet, TCollection>(DbSet<TDbSet> dbSet, PropertyInfo collectionPropInfo)
    where TDbSet : class, new()
    where TCollection : class, new()
        {
            // Получаваме типа на основния ентитет и на колекцията.
            Type entityType = typeof(TDbSet);
            Type collectionType = typeof(TCollection);

            // Вземаме свойствата, които са primary key за колекцията.
            IEnumerable<PropertyInfo> collectionPrimaryKeys = collectionType
                .GetProperties()
                .Where(pi => pi.HasAttribute<KeyAttribute>());

            // Вземаме външния ключ от колекцията, който сочи към основния ентитет.
            PropertyInfo foreignKey = collectionType
                .GetProperties()
                .First(pi => pi.HasAttribute<ForeignKeyAttribute>() &&
                                    collectionType
                                         .GetProperty(pi.GetCustomAttribute<ForeignKeyAttribute>()!.Name)!
                                         .PropertyType == entityType);

            // Вземаме primary key от основния ентитет.
            PropertyInfo primaryKey = entityType
                .GetProperties()
                .First(pi => pi.HasAttribute<KeyAttribute>());

            // Получаваме DbSet за колекцията.
            DbSet<TCollection> navDbSet = (DbSet<TCollection>)
                this.dbSetProperties[collectionType]
                    .GetValue(this)!;

            // За всеки ентитет в основния DbSet, търсим съответните елементи в навигационната колекция.
            foreach (TDbSet dbSetEntity in dbSet)
            {
                // Вземаме стойността на primary key на текущия ентитет.
                object pkValue = primaryKey.GetValue(dbSetEntity)!;

                // Извличаме всички елементи от колекцията, които имат външния ключ, съвпадащ с primary key на основния ентитет.
                IEnumerable<TCollection> navCollectionEntities = navDbSet
                    .Where(navEntity => foreignKey.GetValue(navEntity) != null &&
                                                    foreignKey.GetValue(navEntity)!.Equals(pkValue))
                    .ToArray();

                // Заменяме стойността на навигационното свойство в текущия ентитет.
                ReflectionHelper.ReplaceBackingField(dbSetEntity, collectionPropInfo.Name, navCollectionEntities);
            }
        }
    }
}
