ENG VERSION:

    `MethodInfo`, `PropertyInfo` и `BindingFlags`.

---

### **1. `MethodInfo`**

`MethodInfo` is a class from `System.Reflection` that provides information about the methods of a given type in C#. This includes the method name, its parameters, return values, and various attributes of the method. It allows for dynamically invoking methods during runtime, even without knowing their exact names or types beforehand.

#### How does `MethodInfo` work?

With `MethodInfo`, you can:
-Get information about the methods of a class.
-Dynamically invoke methods during runtime using reflection.

#### `MethodInfo` Example

```csharp
using System;
using System.Reflection;

class Example
{
    private void SayHello(string name)
    {
        Console.WriteLine($"Hello, {name}!");
    }

    static void Main()
    {
        Example example = new Example();
        MethodInfo method = typeof(Example).GetMethod("SayHello", BindingFlags.NonPublic | BindingFlags.Instance);

        // Dynamically invoking the method and passing a value for the parameter
        method.Invoke(example, new object[] { "John" });
    }
}
```

**Explanation * *:
-We use `GetMethod` to get information about the `SayHello` method from the `Example` class.
-The `BindingFlags.NonPublic` and `BindingFlags.Instance` flags are used to specify that we want to find a non-public and instance(not static) method.
- We then use the `Invoke` method to dynamically call the method and pass `"John"` as the argument.

---

### **2. `PropertyInfo`**

`PropertyInfo` is a class that provides information about the properties of a class. With it, you can:
-Get information about properties (e.g., type, name).
- Get or set property values dynamically.

#### `PropertyInfo` Example

```csharp
using System;
using System.Reflection;

class Example
{
    public string Name { get; set; }

    static void Main()
    {
        Example example = new Example();
        PropertyInfo property = typeof(Example).GetProperty("Name");

        // Setting the value of the "Name" property via PropertyInfo
        property.SetValue(example, "Alice");

        // Reading the value of the "Name" property
        Console.WriteLine(property.GetValue(example));  // Output: Alice
    }
}
```

**Explanation * *:
-Through `GetProperty`, we retrieve information about the public property `Name` of the `Example` class.
-With the `SetValue` method, we set the value `"Alice"` for that property.
- Using the `GetValue` method, we retrieve the value of the property and print it.

---

### **3. `BindingFlags`**

`BindingFlags` is a type that contains flags used to specify how to search for methods, properties, or fields through reflection. They are extremely useful when you want to clarify the search, for example, whether to include or exclude public methods, instance or static methods, etc.

#### Commonly used `BindingFlags`

- `BindingFlags.Public`: Includes only public members.
- `BindingFlags.NonPublic`: Includes only non-public members (e.g., private, protected).
- `BindingFlags.Instance`: Includes instance members (not static).
- `BindingFlags.Static`: Includes static members.
- `BindingFlags.FlattenHierarchy`: Includes members inherited from base classes.

#### `BindingFlags` Example

```csharp
using System;
using System.Reflection;

class Example
{
    public string Name { get; set; }
    private int age;

    public Example(string name, int age)
    {
        Name = name;
        this.age = age;
    }

    private void DisplayAge()
    {
        Console.WriteLine($"Age: {age}");
    }

    static void Main()
    {
        Example example = new Example("Alice", 30);

        // Accessing private methods and properties through BindingFlags
        MethodInfo method = typeof(Example).GetMethod("DisplayAge", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(example, null);  // Output: Age: 30

        PropertyInfo property = typeof(Example).GetProperty("Name");
        Console.WriteLine(property.GetValue(example));  // Output: Alice
    }
}
```

**Explanation * *:
-With `BindingFlags.NonPublic | BindingFlags.Instance`, we find the private instance method `DisplayAge`.
- Using `GetProperty`, we find the public property `Name` and print its value.
- `BindingFlags` allows us to specify whether we want public or non-public methods/ properties, and whether they are instance or static.

---

### Summary:

-**`MethodInfo`**: Used for dynamically invoking methods of a class.
-**`PropertyInfo`**: Used to access and set values of properties of a class.
-**`BindingFlags`**: Flags that specify how members (methods, properties, etc.) should be searched via reflection – whether they are public or non-public, instance or static, and so on.

These tools are very powerful and often used in situations where you need to work with classes and objects dynamically, without knowing their types beforehand.







BG VERSION:

`MethodInfo`, `PropertyInfo` и `BindingFlags`.

---

### **1. `MethodInfo`**

`MethodInfo` е клас от `System.Reflection`, който предоставя информация за методите на даден тип в C#. 
Това включва името на метода, неговите параметри, връщаните стойности, както и различни атрибути на метода. 
Той позволява динамично извикване на методи по време на изпълнение на програмата, без да е нужно да познаваш техните конкретни имена или типове предварително.

#### Как работи `MethodInfo`?

Чрез `MethodInfo` можеш да:
-Получиш информация за методите на даден клас.
- Извикаш методи динамично по време на изпълнение, като използваш рефлексия.
  
#### Пример за `MethodInfo`

```csharp
using System;
using System.Reflection;

class Example
{
    private void SayHello(string name)
    {
        Console.WriteLine($"Hello, {name}!");
    }

    static void Main()
    {
        Example example = new Example();
        MethodInfo method = typeof(Example).GetMethod("SayHello", BindingFlags.NonPublic | BindingFlags.Instance);

        // Извикваме метода динамично, като подадем стойност за параметъра
        method.Invoke(example, new object[] { "John" });
    }
}
```

**Обяснение * *:
-Използваме `GetMethod`, за да получим информация за метода `SayHello` от клас `Example`.
- С флаговете `BindingFlags.NonPublic` и `BindingFlags.Instance` заявяваме, че искаме да намерим метод, който не е публичен и е инстанционен (не статичен).
- След това използваме метода `Invoke`, за да извикаме метода динамично и да подадем стойността `"John"` като аргумент.

---

### **2. `PropertyInfo`**

`PropertyInfo` е клас, който предоставя информация за свойствата на даден клас. Чрез него можем да:
-Вземем информация за свойствата (напр. тип, име).
- Задаваме или вземаме стойности на свойствата динамично.

#### Пример за `PropertyInfo`

```csharp
using System;
using System.Reflection;

class Example
{
    public string Name { get; set; }

    static void Main()
    {
        Example example = new Example();
        PropertyInfo property = typeof(Example).GetProperty("Name");

        // Задаваме стойност на свойството "Name" чрез PropertyInfo
        property.SetValue(example, "Alice");

        // Четем стойността на свойството "Name"
        Console.WriteLine(property.GetValue(example));  // Изход: Alice
    }
}
```

**Обяснение * *:
-Чрез `GetProperty` получаваме информация за публичното свойство `Name` на клас `Example`.
- Чрез метода `SetValue` задаваме стойността `"Alice"` на това свойство.
- Чрез метода `GetValue` вземаме стойността на свойството и я отпечатваме.

---

### **3. `BindingFlags`**

`BindingFlags` е тип, който съдържа флагове, указващи как да се търсят методи, свойства или полета чрез рефлексия. Те са изключително полезни, когато искаш да уточниш търсенето, например дали да включиш или изключиш публични методи, инстанционни или статични методи, и т.н.

#### Често използвани `BindingFlags`

- `BindingFlags.Public`: Включва само публични членове.
- `BindingFlags.NonPublic`: Включва само непублични членове (например private, protected).
- `BindingFlags.Instance`: Включва инстанционни членове (не статични).
- `BindingFlags.Static`: Включва статични членове.
- `BindingFlags.FlattenHierarchy`: Включва членове, наследени от базовите класове.

#### Пример за използване на `BindingFlags`

```csharp
using System;
using System.Reflection;

class Example
{
    public string Name { get; set; }
    private int age;

    public Example(string name, int age)
    {
        Name = name;
        this.age = age;
    }

    private void DisplayAge()
    {
        Console.WriteLine($"Age: {age}");
    }

    static void Main()
    {
        Example example = new Example("Alice", 30);

        // Извикваме частни методи и свойства чрез BindingFlags
        MethodInfo method = typeof(Example).GetMethod("DisplayAge", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(example, null);  // Изход: Age: 30

        PropertyInfo property = typeof(Example).GetProperty("Name");
        Console.WriteLine(property.GetValue(example));  // Изход: Alice
    }
}
```

**Обяснение * *:
-С `BindingFlags.NonPublic | BindingFlags.Instance` намираме частния инстанционен метод `DisplayAge`.
- С `GetProperty` намираме публичното свойство `Name` и извеждаме стойността му.
- `BindingFlags` дава възможност да уточним дали искаме публични или частни методи/свойства, както и дали те са инстанционни или статични.

---

### Обобщение:

- **`MethodInfo`**: Използва се за динамично извикване на методи на даден клас.
- **`PropertyInfo`**: Използва се за достъп и задаване на стойности на свойства на даден клас.
- **`BindingFlags`**: Флагове, които уточняват как да се търсят членове (методи, свойства и т.н.) чрез рефлексия – дали са публични или частни, инстанционни или статични и др.

Тези инструменти са много мощни и често се използват в ситуации, в които трябва да работим с класове и обекти по динамичен начин, без да познаваме типовете предварително.
