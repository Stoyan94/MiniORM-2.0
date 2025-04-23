using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace MiniORM
{
    public class ChangeTracker<T>
        where T : class, new()
    {
        // Този клас проследява промените на обекти от тип T и управлява състоянието на добавени, премахнати и модифицирани записи.

        private readonly ICollection<T> allEntities;
        private readonly ICollection<T> addedEntities;
        private readonly ICollection<T> removedEntities;

        // Полета за съхранение на всички обекти (allEntities), добавени (addedEntities) и премахнати (removedEntities).
        // Типът T е параметър на класа, който ще бъде заместен с конкретен тип клас при използване на ChangeTracker.

        public ChangeTracker(IEnumerable<T> entities)
        {
            this.allEntities = this.CloneEntities(entities);
            // Създава копие на всички entities, за да не променяме оригиналните.

            this.addedEntities = new HashSet<T>();
            // Инициализация на колекцията за добавени обекти.

            this.removedEntities = new HashSet<T>();
            // Инициализация на колекцията за премахнати обекти.
        }

        public IReadOnlyCollection<T> AllEntities
            => this.allEntities.ToList().AsReadOnly();
        // Връща всички обекти, проследявани от ChangeTracker, като неизменяема колекция.

        public IReadOnlyCollection<T> AddedEntities
            => this.addedEntities.ToList().AsReadOnly();
        // Връща всички добавени обекти като неизменяема колекция.

        public IReadOnlyCollection<T> RemovedEntities
            => this.removedEntities.ToList().AsReadOnly();
        // Връща всички премахнати обекти като неизменяема колекция.

        public void Add(T entity)
        {
            this.addedEntities.Add(entity);
        }
        // Добавя обект към премахнатите entities

        public void Remove(T entity)
        {
            this.removedEntities.Add(entity);
        }
        // Премахва обект, като го добавя към премахнатите entities.

        public IEnumerable<T> GetModifiedEntities(DbSet<T> dbSet)
        {
            ICollection<T> modifiedEntities = new HashSet<T>();
            // Създава нова колекция за модифицирани обекти.

            PropertyInfo[] primaryKeys = typeof(T).GetProperties()
                .Where(pi => pi.HasAttribute<KeyAttribute>())
                .ToArray();
            // Използва Reflection, за да вземе всички свойства на типа T, които са означени с атрибута [Key], който се използва за първичните ключове.

            foreach (T proxyEntity in this.allEntities)
            {
                IEnumerable<object> proxyPrimaryKeyValues =
                    GetPrimaryKeyValues(primaryKeys, proxyEntity);
                // Взима стойностите на първичния ключ от proxyEntity.

                T dbSetEntity = dbSet.Entities
                    .Single(e => GetPrimaryKeyValues(primaryKeys, e)
                                           .SequenceEqual(proxyPrimaryKeyValues));
                // Търси същия обект в dbSet по стойностите на първичния ключ. Тук се използва `SequenceEqual` за да се сравнят стойностите на първичния ключ между proxyEntity и dbSetEntity.

                bool isEntityModified = this.IsModified(proxyEntity, dbSetEntity);
                // Проверява дали обектът е модифициран чрез метода IsModified.

                if (isEntityModified)
                {
                    modifiedEntities.Add(dbSetEntity);
                }
                // Ако обектът е модифициран, го добавя към колекцията на модифицираните обекти.
            }

            return modifiedEntities;
        }

        private static IEnumerable<object> GetPrimaryKeyValues(IEnumerable<PropertyInfo> primaryKeys, T entity)
        {
            ICollection<object> primaryKeyValues = new HashSet<object>();
            // Създава колекция за стойностите на първичния ключ.

            foreach (PropertyInfo propertyInfo in primaryKeys)
            {
                object? primaryKeyValue = propertyInfo.GetValue(entity);
                // Взема стойността на първичния ключ за всяко свойство.

                if (primaryKeyValue == null)
                {
                    throw new ArgumentNullException
                        (propertyInfo.Name, ErrorMessages.PrimaryKeyNullErrorMessage);
                    // Ако стойността на първичния ключ е null, хвърля изключение с грешка.
                }

                primaryKeyValues.Add(primaryKeyValue);
            }

            return primaryKeyValues;
        }

        private ICollection<T> CloneEntities(IEnumerable<T> entities)
        {
            ICollection<T> clonedEntities = new HashSet<T>();
            // Създава колекция, в която ще се съхраняват клонираните обекти.

            PropertyInfo[] properties = typeof(T).GetProperties()
                .Where(pi => DbContext.AllowedSqlTypes.Contains(pi.PropertyType))
                .ToArray();
            // Използва Reflection, за да вземе всички свойства на тип T, които са разрешени от DbContext и съдържат валидни SQL типове.

            foreach (T entity in entities)
            {
                T entityClone = Activator.CreateInstance<T>();
                // Създава ново копие на entity.

                foreach (PropertyInfo propertyInfo in properties)
                {
                    object? originalEntityValue = propertyInfo.GetValue(entity);
                    propertyInfo.SetValue(entityClone, originalEntityValue);
                    // За всяко разрешено свойство копира стойността му от оригиналния обект към клонирания.
                }

                clonedEntities.Add(entityClone);
                // Добавя клонирания обект в колекцията.
            }

            return clonedEntities;
        }

        private bool IsModified(T proxyEntity, T dbSetEntity)
        {
            PropertyInfo[] monitoredProperties = typeof(T).GetProperties()
                .Where(pi => DbContext.AllowedSqlTypes.Contains(pi.PropertyType))
                .ToArray();
            // Избира само наблюдаваните свойства от тип T, които имат допустими SQL типове.

            foreach (PropertyInfo pi in monitoredProperties)
            {
                object? proxyEntityValue = pi.GetValue(proxyEntity);
                object? dbSetEntityValue = pi.GetValue(dbSetEntity);
                // Взема стойностите на свойствата от proxyEntity и dbSetEntity.

                if (proxyEntityValue == null &&
                    dbSetEntityValue == null)
                {
                    continue;
                }
                // Ако и двете стойности са null, пропускаме това свойство.

                if (!proxyEntityValue!.Equals(dbSetEntityValue))
                {
                    return true;
                }
                // Ако стойностите на свойствата не съвпадат, връща true, че обектът е модифициран.
            }

            return false;
        }
    }
}
