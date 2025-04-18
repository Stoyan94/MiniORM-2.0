1.DbContext Constructor(Конструктор на DbContext)

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

protected DbContext(string connectionString):

Това е конструктор на класа DbContext, който инициализира връзката към базата данни. Той приема низ с връзка към базата данни.


this.dbConnection = new DatabaseConnection(connectionString);:

Това създава нов обект от тип DatabaseConnection, който ще бъде използван за осъществяване на връзка към базата данни чрез предоставения низ connectionString.


this.dbSetProperties = this.DiscoverDbSet();:

Този ред извиква метода DiscoverDbSet, който намира всички свойства на типа DbSet<T> в този контекст и ги съхранява 
в речник dbSetProperties, където ключът е типът на обекта (например Person),
а стойността е информация за съответното свойство, което е DbSet<T>.


Този блок създава нов обект ConnectionManager, който управлява живота на връзката с базата данни. 
Използва се тук, за да гарантира правилното отваряне и затваряне на връзката към базата данни.


this.InitializeDbSets();:

Този ред извиква метод за инициализация на DbSet обектите. 
Той ще попълни свойствата с реални инстанции от тип DbSet<T>, които ще съдържат данни от базата.


this.MapAllRelations();:

Този метод извиква MapAllRelations, който настройва всички навигационни свойства и релации между различни таблици в базата данни. 
Коментарът в края показва, че тази операция се извършва след затварянето на връзката, защото тя е операция в паметта.





### 2. `SaveChanges()` – Детайлно обяснение

#### Ред 1 - 2: 
```csharp
IEnumerable<object> dbSetsObjects = this.dbSetProperties
    .Select(edb => edb.Value.GetValue(this)!)
    .ToArray();
```
-**Какво прави * *: 
  - `dbSetProperties` е речник, в който се съхраняват всички свойства на `DbSet` за различните типове.
    Тук се избира стойността на тези свойства чрез `edb.Value.GetValue(this)` (получава се стойността на всяко свойство от `DbSet`).

  - Резултатът е масив от всички `DbSet` обекти.
- **Защо го прави**: 
  -За да получи всички `DbSet` инстанции, които са част от `DbContext`.
    Това е основната стъпка, преди да се започне записването в базата данни.
  
#### Ред 3 - 6: 
```csharp
foreach (IEnumerable<object> dbSet in dbSetsObjects)
{
    IEnumerable<object> invalidEntities = dbSet
        .Where(e => !IsObjectValid(e))
        .ToArray();
```
-**Какво прави * *: 
  -Обхожда всички `dbSetObjects` и проверява дали всяко обектно представяне на съществуваща сущност (Entity) е валидно.
  - Използва метода `IsObjectValid()`, за да провери дали даден обект е валиден спрямо стандартите за валидация.
  
#### Ред 7 - 9:
```csharp
if (invalidEntities.Any())
{
    throw new InvalidOperationException(string.Format(InvalidEntitiesInDbSetMessage,
        invalidEntities.Count(), dbSet.GetType().Name));
}
```
-**Какво прави * *:
  -Ако има невалидни ентитети, генерира изключение, което съдържа съобщение за грешка.
- **Защо го прави**:
  -За да предотврати изпълнението на последващите стъпки, ако има невалидни данни в `DbSet`.

#### Ред 10 - 12:
```csharp
using (new ConnectionManager(this.dbConnection))
{
    using SqlTransaction transaction = this.dbConnection
        .StartTransaction();
```
-**Какво прави * *:
  -Използва `ConnectionManager`, за да управлява живота на връзката към базата данни.
  - Започва нова транзакция с помощта на `SqlTransaction`.
  
#### Ред 13 - 24: (вътре в транзакцията)
```csharp
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
```
-**Какво прави * *:
  -За всеки `dbSet`, извиква метода `Persist`, който изпълнява действия за запис в базата данни.
  
  -Използва reflection за извикване на метода `Persist` чрез `MethodInfo`:
 
  - `MethodInfo persistMethod = typeof(DbContext).GetMethod("Persist", BindingFlags.NonPublic | BindingFlags.Instance)` намира метода `Persist` от тип `DbContext`.
  
  - `MakeGenericMethod(dbSet.GetType())` прави метода generic, за да може да работи с типизацията на съответния `DbSet`.
  
  -В случая, ако възникне грешка при извикването, транзакцията се връща назад чрез `transaction.Rollback()`.
  
  -След това се извършва `transaction.Commit()`, за да се запишат промените в базата данни.

#### Reflection с `MethodInfo`:
-**`MethodInfo`**:
  -Използва се за динамично извикване на методи, които не могат да бъдат достъпни по обикновен начин. 
        В този случай, методът `Persist` е извикан чрез reflection. Това е необходимо, 
        защото типовете `DbSet` могат да бъдат различни, и трябва да се използва генератичен метод за всяка от тези типизации.
 
  - **Какво ще тригери**: Тригерира извикването на метода `Persist`, който извършва същинската работа за добавяне, обновяване или изтриване на записи в базата данни.

#### Діаграма за `SaveChanges` метод:

```plaintext
+ --------------------------------------+
| SaveChanges() |
+--------------------------------------+
                 |
                 v
+ -----------------------------+--------------------+
| Loop through all dbSetsObjects |
+-----------------------------+--------------------+
                 |
                 v
+ -----------------------------+--------------------+
| Check for invalid entities                        |
+-----------------------------+--------------------+
                 |
                 v
+ -----------------------------+--------------------+
| Start transaction with database connection |
+-----------------------------+--------------------+
                 |
                 v
+ ----------------------------------------+
| Reflection - Call Persist Method |
+----------------------------------------+
                 |
                 v
+ ----------------------------------------+
| Commit or Rollback transaction |
+----------------------------------------+
```

Тази диаграма показва процеса на запазване на промените в базата данни, като включва основните стъпки и вътрешните цикли на метода.

-- -




3.IsObjectValid(Проверка на валидността на обект)

private static bool IsObjectValid(object obj)
        {
            ValidationContext validationContext = new ValidationContext(obj);
            ICollection<ValidationResult> validationErros = new List<ValidationResult>();

            return Validator
                .TryValidateObject(obj, validationContext, validationErros, true);
        }

private static bool IsObjectValid(object obj):

Този метод проверява дали даден обект е валиден, като използва вградената в.NET валидация.


ValidationContext validationContext = new ValidationContext(obj);:

Създава контекст за валидацията на обекта, като се задава самият обект.


ICollection<ValidationResult> validationErros = new List<ValidationResult>();:

Създава списък, който ще съдържа резултатите от валидацията.


return Validator.TryValidateObject(obj, validationContext, validationErros, true);:

Извършва валидация на обекта и връща true, ако обектът е валиден, или false, ако не е.





### 1. **`Persist<TEntity>`**
```csharp
private void Persist<TEntity>(DbSet<TEntity> dbSet)
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
```

-**`private void Persist<TEntity>(DbSet<TEntity> dbSet)`**:
  -Това е метод, който съхранява промените за даден `DbSet<TEntity>`. 
    Той работи само с обекти от тип `TEntity` и ги обработва в зависимост от това дали са добавени, променени или премахнати от контекста.

- **`string tableName = this.GetTableName(typeof(TEntity));`**:
  -Извиква се метод `GetTableName`, който връща името на таблицата в базата данни, свързано с типа `TEntity`.

- **`IEnumerable<string> columnsNames = this.dbConnection.FetchColumnNames(tableName);`**:
  -Взимат се имената на колоните от таблицата, която съответства на типа `TEntity`.

- **`if (dbSet.ChangeTracker.AddedEntities.Any())`**:
  -Проверява дали има нови добавени записи в контекста. 
    Ако има, те се записват в базата данни чрез метода `InsertEntities`.

- **`IEnumerable<TEntity> modifiedEntities = dbSet.ChangeTracker.GetModifiedEntities(dbSet);`**:
  -Извличат се променените обекти в текущия `DbSet<TEntity>`.

- **`if (modifiedEntities.Any())`**:
  -Ако има променени обекти, те се обновяват в базата чрез метода `UpdateEntities`.

- **`if (dbSet.ChangeTracker.RemovedEntities.Any())`**:
  -Проверява дали има премахнати обекти. Ако има, те се изтриват от базата чрез метода `DeleteEntities`.

### 2. **`PopulateDbSet<TEntity>`**
```csharp
private void PopulateDbSet<TEntity>(PropertyInfo dbSetPropertyInfo)
    where TEntity : class, new()
{
    IEnumerable<TEntity> dbSetEntities = this.LoadTableEntities<TEntity>();
    DbSet<TEntity> dbSetInstance = new DbSet<TEntity>(dbSetEntities);
    ReflectionHelper.ReplaceBackingField(this, dbSetPropertyInfo.Name, dbSetInstance);
}
```

-**`private void PopulateDbSet<TEntity>(PropertyInfo dbSetPropertyInfo)`**:
  -Този метод попълва свойствата на типа `DbSet<TEntity>`, като извлича всички съществуващи записи от базата данни и ги добавя към контекста.

- **`IEnumerable<TEntity> dbSetEntities = this.LoadTableEntities<TEntity>();`**:
  -Извиква се методът `LoadTableEntities`, който връща всички записи за съответния тип `TEntity` от базата данни.

- **`DbSet<TEntity> dbSetInstance = new DbSet<TEntity>(dbSetEntities);`**:
  -Създава нов екземпляр на `DbSet<TEntity>`, който съдържа заредените от базата данни обекти.

- **`ReflectionHelper.ReplaceBackingField(this, dbSetPropertyInfo.Name, dbSetInstance);`**:
  -Използва Reflection, за да замени вътрешното поле (backing field) на съответното свойство с новия `DbSet<TEntity>`. Това се прави чрез помощния клас `ReflectionHelper`.

### 3. **`LoadTableEntities<TEntity>`**
```csharp
private IEnumerable<TEntity> LoadTableEntities<TEntity>()
    where TEntity : class
{
    Type tableType = typeof(TEntity);
    IEnumerable<string> columnNames = this.GetEntityColumnNames(tableType);
    string tableName = this.GetTableName(tableType);

    return this.dbConnection
        .FetchResultSet<TEntity>(tableName, columnNames.ToArray());
}
```

-**`private IEnumerable<TEntity> LoadTableEntities<TEntity>()`**:
  -Метод за зареждане на всички записи от таблицата, която съответства на типа `TEntity`.

- **`Type tableType = typeof(TEntity);`**:
  - Получава се типа на `TEntity`, който ще се използва за извличане на информация за колоните и името на таблицата.

- **`IEnumerable<string> columnNames = this.GetEntityColumnNames(tableType);`**:
  - Извиква се методът `GetEntityColumnNames`, който извлича имената на колоните от таблицата, свързана с `TEntity`.

- **`string tableName = this.GetTableName(tableType);`**:
  - Извлича се името на таблицата, свързано с типа `TEntity`.

- **`return this.dbConnection.FetchResultSet<TEntity>(tableName, columnNames.ToArray());`**:
  - Извиква се метод за извличане на всички записи от таблицата, като се използва името на таблицата и имената на колоните.

### 4. **`GetEntityColumnNames`**
```csharp
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
```

- **`private IEnumerable<string> GetEntityColumnNames(Type entityType)`**:
  - Метод за получаване на имената на колоните на таблицата, свързана с типа `TEntity`.

- **`string tableName = this.GetTableName(entityType);`**:
  - Извлича името на таблицата, свързано с типа `TEntity`.

- **`IEnumerable<string> tableColumnNames = this.dbConnection.FetchColumnNames(tableName);`**:
  - Извличат се имената на колоните от базата данни за съответната таблица.

- **`IEnumerable<string> entityColumnNames = entityType.GetProperties()...`**:
  - Извличат се всички свойства на типа `TEntity`, които са съвпадение с колоните в таблицата, не са помечени с атрибута `NotMapped` и са от тип, който може да се използва в SQL (чрез проверка с `AllowedSqlTypes`).

### 5. **`GetTableName`**
```csharp
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
```

- **`private string GetTableName(Type tableType)`**:
  - Метод за извличане на името на таблицата, свързано с типа на обекта.

- **`Attribute? tableNameAtr = Attribute.GetCustomAttribute(tableType, typeof(TableAttribute));`**:
  - Използва се Reflection, за да се получи атрибутът `TableAttribute`, който съдържа името на таблицата.

- **`if (tableNameAtr == null)`**:
  - Ако не е намерен атрибутът, се връща името на съответното свойство от `dbSetProperties`.

- **`if (tableNameAtr is TableAttribute tableNameAttrConf)`**:
  - Ако атрибутът е намерен и е от тип `TableAttribute`, връща се името на таблицата, съдържащо се в атрибута.

- **`throw new ArgumentException(String.Format(NoTableNameFound, this.dbSetProperties[tableType].Name));`**:
  - Ако не може да бъде намерено име на таблица, се хвърля изключение.

---
Тези методи показват как се използва Reflection, за да се изследват и манипулират свойствата на класовете, 
как се работи с базата данни чрез DbSet и как се картографират релации между различни типове обекти в контекста на обектно-релационни мапери (ORM)




Разбивам ти всеки ред от двата метода **с ултра подробни обяснения**, както пожела, мастер. Пускам ти първо метода `DiscoverDbSet`, а после и `InitializeDbSets`.

---

## 🔍 Метод `DiscoverDbSet`

```csharp
private IDictionary<Type, PropertyInfo> DiscoverDbSet()
```
- **Създава метод** с име `DiscoverDbSet`.
- Връща **речник (dictionary)**, където:
  - ключът е типът на ентитета (`Type`),
  - стойността е `PropertyInfo` – информация за свойството `DbSet<>`, което го представя.

```csharp
return this.GetType()
```
- `this.GetType()` връща **типа на текущия обект** (например `MyAppDbContext`, ако наследява `DbContext`).

```csharp
    .GetProperties()
```
- Взима **всички свойства** на този тип – т.е. всичко, което е декларирано като `public DbSet<T> MyEntities { get; set; }`.

```csharp
    .Where(pi => pi.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
```
- **Филтрира** само онези свойства, които са **от тип `DbSet<>`**.
- `PropertyInfo.PropertyType` връща типа на самото свойство (напр. `DbSet<User>`).
- `GetGenericTypeDefinition()` изважда "основния шаблонен тип" – ще върне `DbSet<>`.
- Сравняваме го с `typeof(DbSet<>)`, за да сме сигурни, че това е нужният тип.

```csharp
    .ToDictionary(
        pi => pi.PropertyType.GetGenericArguments().First(),
        pi => pi
    );
```
- Превръщаме списъка от `PropertyInfo` обекти в речник.
- `pi.PropertyType.GetGenericArguments().First()` връща типа вътре в `DbSet<T>` – т.е. `T`.
- Ключът на речника става `T` (типът на ентитета), стойността е `PropertyInfo` за самото `DbSet<T>`.

---
### ✅ Пример:
```csharp
public DbSet<User> Users { get; set; }
public DbSet<Post> Posts { get; set; }
```

Резултат:
```csharp
{
    typeof(User) => PropertyInfo за Users,
    typeof(Post) => PropertyInfo за Posts
}
```

---

## ⚙️ Метод `InitializeDbSets`

```csharp
private void InitializeDbSets()
```
- Метод, който се грижи за **инициализацията на всички `DbSet<T>`** в контекста, чрез reflection.

```csharp
foreach (KeyValuePair<Type, PropertyInfo> dbSetKvp in dbSetProperties)
```
- Обхожда всеки елемент от речника `dbSetProperties`, който съдържа:
  - `Key` – типът на ентитета (`T`),
  - `Value` – `PropertyInfo` за съответното `DbSet<T>`.

```csharp
    Type dbSetType = dbSetKvp.Key;
```
- Извличаме **типа на ентитета** (`T`) от текущата двойка в речника.

```csharp
    PropertyInfo dbSetPoperty = dbSetKvp.Value;
```
- Взимаме `PropertyInfo` обекта за `DbSet<T>`, за да знаем върху кое свойство ще работим.

```csharp
    MethodInfo populateDbSetMethodInfo = typeof(DbContext)
        .GetMethod("PopulateDbSet", BindingFlags.NonPublic | BindingFlags.Instance)!
        .MakeGenericMethod(dbSetType);
```
- Взимаме метода `PopulateDbSet`, който е **непубличен (private/protected)** и инстанциен (не е static).
- `MakeGenericMethod(dbSetType)` го прави **generic** – т.е. превръща `PopulateDbSet<T>()` в `PopulateDbSet<User>()`, `PopulateDbSet<Post>()` и т.н.

```csharp
    populateDbSetMethodInfo.Invoke(this, new object[] { dbSetPoperty });
```
- Извикваме този generic метод върху текущия контекст (`this`).
- Подаваме му `PropertyInfo`-то на съответния `DbSet` – това му е аргументът.

---

### ✅ Целият поток:

1. `DiscoverDbSet()` – открива всички `DbSet<T>` свойства и ги записва по тип.
2. `InitializeDbSets()` – за всеки открит `DbSet<T>`, извиква метода `PopulateDbSet<T>(PropertyInfo)`.

---




---

## 🔹 Метод: `MapRelations<TEntity>`

```csharp
private void MapRelations<TEntity>(DbSet<TEntity> dbSet)
    where TEntity : class, new()
```

🧠 **Какво прави методът:**  
Този метод се грижи за установяване на връзките (релациите) между обекти от даден тип `TEntity`, като:

- Мапва (свързва) навигационните свойства (референции към други обекти, указани чрез външни ключове);
- Мапва колекциите (напр. `ICollection<Order>` в `Customer`), които сочат към множествени ентитети.

---

```csharp
Type entityType = typeof(TEntity);
```

📌 **Какво става тук:**  
Извличаме типа на текущия ентитет (клас), с който работим – напр. `Customer`, `Product`, `Order`. 
Това ни трябва за по-нататъшно рефлексивно извличане на неговите свойства.

---

```csharp
this.MapNavigationProperties(dbSet);
```

📌 **Какво става тук:**  
Извикваме метода `MapNavigationProperties`, който:

- намира всички свойства с атрибут `[ForeignKey]` в класа `TEntity`;
- и ги свързва със съответните обекти в други `DbSet`-и.

🔁 Това е първата стъпка от свързването на ентитета с други ентитети чрез външни ключове.

---

```csharp
IEnumerable<PropertyInfo> entityCollections = entityType
    .GetProperties()
    .Where(pi => pi.PropertyType.IsGenericType &&
                 pi.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>));
```

📌 **Какво става тук:**  
Чрез Reflection:

- Извличаме всички свойства на ентитета;
- Филтрираме тези, които са от тип `ICollection<T>` – това са множествените релации (one-to-many или many-to-many).

📍 Пример: ако в `Customer` има `public ICollection<Order> Orders`, това свойство ще бъде уловено тук.

---

```csharp
foreach (PropertyInfo entityCollectionPropInfo in entityCollections)
```

🔁 **Започваме цикъл:**  
За всяко такова колекционно свойство (напр. `Orders`), ще мапнем съдържанието му.

---

```csharp
Type collectionEntityType = entityCollectionPropInfo
    .PropertyType
    .GenericTypeArguments
    .First();
```

📌 **Какво става тук:**  
Извличаме типа на обектите вътре в колекцията. Пример: ако имаме `ICollection<Order>`, тук ще получим `Order`.

---

```csharp
MethodInfo mapCollectionGenMethodInfo = typeof(DbContext)
    .GetMethod("MapCollection", BindingFlags.Instance | BindingFlags.NonPublic)!
    .MakeGenericMethod(entityType, collectionEntityType);
```

🧠 **Reflection:**  
- Взимаме метода `MapCollection`, който е `private` и generic.
- Създаваме конкретен вариант от него за дадените `TEntity` и `TCollection`.

🛠️ Тук използваме `MakeGenericMethod`, за да го направим типизиран – напр. `MapCollection<Customer, Order>`.

---

```csharp
mapCollectionGenMethodInfo.Invoke(this, new object[] { dbSet, entityCollectionPropInfo });
```

🔧 **Извикваме метода:**  
- Подаваме му текущия `dbSet` и свойството, което представлява колекция;
- Методът `MapCollection` ще се погрижи да събере обектите от съответния `DbSet<T>` и да ги постави в това свойство.

---

✅ **Край на първия метод. Резюме:**
- `MapRelations` върши двойна работа:
  - свързва единични навигационни свойства чрез `MapNavigationProperties`;
  - свързва колекционни свойства чрез `MapCollection`.

---




---

## 🔹 Метод: `MapNavigationProperties<TEntiy>`

```csharp
private void MapNavigationProperties<TEntiy>(DbSet<TEntiy> dbSet)
        where TEntiy : class, new()
```

🧠 **Какво прави методът:**  
Мапва навигационните свойства (единични връзки – one-to-one и many-to-one) за дадения `DbSet<T>`.  
Навигационното свойство е полето, което представя свързан обект чрез външен ключ (`ForeignKey`).

---

```csharp
Type entityType = typeof(TEntiy);
```

📌 **Какво прави:**  
Чрез Reflection взимаме типа на ентитета – напр. `Order`, `Customer`, и т.н.

---

```csharp
IEnumerable<PropertyInfo> foreignKeys = entityType
    .GetProperties()
    .Where(pi => pi.HasAttribute<ForeignKeyAttribute>());
```

📌 **Какво прави:**  
- Извличаме всички свойства, които имат атрибута `[ForeignKey]`;
- Това са полетата, които сочат към Primary Key на друг обект (напр. `CustomerId` в `Order`).

---

```csharp
foreach (PropertyInfo fkPropertyInfo in foreignKeys)
```

🔁 **Цикъл по всяко FK свойство:**  
Ще обработим всяко `ForeignKey` поле и ще мапнем навигационното му свойство.

---

```csharp
string navigationPropName = fkPropertyInfo
    .GetCustomAttribute<ForeignKeyAttribute>()!.Name;
```

📌 **Какво прави:**  
- Вземаме името на навигационното свойство, към което FK е прикрепен.
- Това име се подава чрез атрибута `[ForeignKey("NavigationPropertyName")]`.

---

```csharp
PropertyInfo? navigationPropertyInfo = entityType
    .GetProperty(navigationPropName);
```

📌 **Какво прави:**  
Опитваме се да намерим самото навигационно свойство (напр. `Customer` в `Order`), по името, получено от атрибута.

---

```csharp
if (navigationPropertyInfo == null)
{
    throw new ArgumentException(String.Format(InvalidNavigationPropertyName,
        fkPropertyInfo.Name, navigationPropName));
}
```

🚨 **Проверка:**  
Ако не сме намерили навигационното свойство – хвърляме грешка, защото не можем да свържем FK с нищо.

---

```csharp
object? navDbSetInstance =
    this.dbSetProperties[navigationPropertyInfo.PropertyType].GetValue(this);
```

📌 **Какво прави:**  
- От речника `dbSetProperties` взимаме съответния `DbSet` за навигационния тип (напр. ако `Customer` е типът, ще вземем `DbSet<Customer>`);
- Използваме Reflection, за да го достъпим.

---

```csharp
if (navDbSetInstance == null)
{
    throw new ArgumentException(String.Format(NavPropertyWithoutDbSetMessage,
        navigationPropName, navigationPropertyInfo.PropertyType));
}
```

🚨 **Проверка:**  
Ако не сме намерили `DbSet` за навигационния обект, няма как да направим връзка, затова хвърляме грешка.

---

```csharp
PropertyInfo navEntityPkPropInfo = navigationPropertyInfo
 .PropertyType
 .GetProperties()
 .First(pi => pi.HasAttribute<KeyAttribute>());
```

📌 **Какво прави:**  
- Взимаме `Primary Key` свойството на навигационния обект (напр. `Customer.Id`);
- Това ни е нужно, за да намерим правилния обект по стойността на FK.

---

```csharp
foreach (TEntiy entity in dbSet)
```

🔁 **Цикъл:**  
Минаваме през всеки обект от текущия `DbSet<T>` – напр. всеки `Order`.

---

```csharp
object? fkValue = fkPropertyInfo.GetValue(entity);
```

📌 **Какво прави:**  
Чрез Reflection взимаме стойността на външния ключ – напр. `CustomerId = 5`.

---

```csharp
if (fkValue == null)
{
    navigationPropertyInfo.SetValue(entity, null);
    continue;
}
```

🧹 **Проверка:**  
Ако няма стойност за FK (т.е. null), не можем да свържем нищо → задаваме `null` в навигационното свойство.

---

```csharp
object? navPropValueEntity = ((IEnumerable<object>)navDbSetInstance)
    .First(currNavPropEntity => navEntityPkPropInfo
        .GetValue(currNavPropEntity)!
        .Equals(fkValue));
```

🔍 **Търсене:**  
- Минаваме през всички обекти от навигационния `DbSet` (напр. `DbSet<Customer>`);
- Сравняваме техния PK с FK стойността;
- Когато съвпадне – това е обектът, който трябва да присъства в навигационното поле.

---

```csharp
navigationPropertyInfo.SetValue(entity, navPropValueEntity);
```

🔧 **Задаване:**  
Поставяме намерения обект в навигационното свойство – напр. `Order.Customer = този, който има Id = CustomerId`.

---

✅ **Край на втория метод. Резюме:**
- Мапва всички единични навигационни свойства чрез `[ForeignKey]`;
- Прави го чрез Reflection и търсене по PK стойности в съответните `DbSet`-и.

---

Сега преминаваме към третия метод, както обещах 💪

---

## 🔹 Метод: `MapCollection<TDbSet, TCollection>`

```csharp
private void MapCollection<TDbSet, TCollection>(DbSet<TDbSet> dbSet, PropertyInfo collectionPropInfo)
    where TDbSet : class, new()
    where TCollection : class, new()
```

🧠 **Какво прави методът:**  
Свързва колекциите от тип `ICollection<T>` (напр. `Customer.Orders`) чрез външния ключ от колекцията към главния ентитет.

---

```csharp
Type entityType = typeof(TDbSet);
Type collectionType = typeof(TCollection);
```

📌 **Какво прави:**  
Взимаме типовете на основния ентитет (напр. `Customer`) и на колекционния обект (напр. `Order`).

---

```csharp
IEnumerable<PropertyInfo> collectionPrimaryKeys = collectionType
    .GetProperties()
    .Where(pi => pi.HasAttribute<KeyAttribute>());
```

📌 **Какво прави:**  
Извличаме всички `Primary Key` свойства на обекта в колекцията – обикновено това ще е едно поле (напр. `Order.Id`).

---

```csharp
PropertyInfo foreignKey = collectionType
    .GetProperties()
    .First(pi => pi.HasAttribute<ForeignKeyAttribute>() &&
                        collectionType
                             .GetProperty(pi.GetCustomAttribute<ForeignKeyAttribute>()!.Name)!
                             .PropertyType == entityType);
```

📌 **Какво прави:**  
- Намираме това свойство от колекционния обект, което представлява външния ключ към главния обект;
- Пример: в `Order`, това ще е `CustomerId`.

🔐 Внимание: Проверяваме и дали свойството, към което FK сочи, е от типа на главния ентитет (`Customer`).

---

```csharp
PropertyInfo primaryKey = entityType
    .GetProperties()
    .First(pi => pi.HasAttribute<KeyAttribute>());
```

📌 **Какво прави:**  
Взимаме `Primary Key` на главния ентитет – напр. `Customer.Id`.

---

```csharp
DbSet<TCollection> navDbSet = (DbSet<TCollection>)
    this.dbSetProperties[collectionType]
        .GetValue(this)!;
```

📌 **Какво прави:**  
Взимаме `DbSet`-а за колекционния обект (напр. `DbSet<Order>`).

---

```csharp
foreach (TDbSet dbSetEntity in dbSet)
```

🔁 **Цикъл по основните обекти:**  
Минаваме през всеки `Customer`, например.

---

```csharp
object pkValue = primaryKey.GetValue(dbSetEntity)!;
```

📌 **Какво прави:**  
Взимаме стойността на `Primary Key` на текущия главен обект.

---

```csharp
IEnumerable<TCollection> navCollectionEntities = navDbSet
    .Where(navEntity => foreignKey.GetValue(navEntity) != null &&
                                    foreignKey.GetValue(navEntity)!.Equals(pkValue))
    .ToArray();
```

📌 **Какво прави:**  
- От `DbSet<Order>` взимаме всички обекти, където `CustomerId == pkValue`;
- Тоест, всички поръчки за текущия клиент.

---

```csharp
ReflectionHelper.ReplaceBackingField(dbSetEntity, collectionPropInfo.Name, navCollectionEntities);
```

🔧 **Какво прави:**  
С помощта на `ReflectionHelper`:

- Замества съдържанието на колекционното свойство (`Orders`) с намерените обекти;
- Това става директно в backing field-а, дори ако свойството няма setter.

---

✅ **Край на третия метод. Резюме:**
- `MapCollection` свързва един `TEntity` с всички негови `TCollection` обекти;
- Използва външен ключ от колекцията към основния обект;
- Поставя колекцията в навигационното свойство чрез Reflection.

---
