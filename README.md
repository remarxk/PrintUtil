# PrintUtil
该库使用表达式树和缓存的实现了对象信息的打印，其性能效率要远高于现有的打印库ObjectDumper和Newtonsoft.Json库。

## 特性
1.提供IgnorePrintAttribue。如果该Attribue标识在字段或属性上，则该字段或属性将会被忽略打印。如果该Attribue标识在ToString方法上时，则在打印该对象时会忽略对象的ToString方法实现，使用默认的打印显示。<br>
2.支持重写ToString后的打印显示，如果一个对象的ToString方法被重写了，则在打印对象时会优先使用ToString方法去打印该对象，如果没有重写ToString方法则会使用默认的打印显示。

## 使用方法
### 对象的定义
```C#
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    [IgnorePrint] public double Price { get; set; }
    public Profession Profession { get; set; }
    public Skill[] Skills { get; set; }
    public List<Histroy> Histroy { get; set; }

    [IgnorePrint]
    public override string ToString()
    {
        return this.Print();
    }
}

public class Skill
{
    public string Name { get; set; }
    public double Damage { get; set; }

    public Skill(string name, double damage)
    {
        Name = name;
        Damage = damage;
    }
}

public class Profession
{
    public string Name { get; set; }
    public double Income { get; set; }

    public Profession(string name, double income)
    {
        Name = name;
        Income = income;
    }

    public override string ToString()
    {
        return $"Profession{{名称={Name},收入={Income}}}";
    }
}

public struct Histroy
{
    public DateTime Time { get; set; }
    public string Content { get; set; }

    public Histroy(DateTime time, string content)
    {
        Time = time;
        Content = content;
    }
}
```
### 使用
```C#
Person person = new Person()
{
    Id = 1,
    Name = "张三",
    Age = 30,
    Price = 150.2,
    Profession = new Profession("律师", 30000.58),
    Skills = new Skill[]
        {
            new Skill("技能1", 20.5),
            new Skill("技能2", 58.4)
        },
    Histroy = new List<Histroy>
        {
            new Histroy(DateTime.Now, "睡觉"),
            new Histroy(Convert.ToDateTime("2022/3/15 8:30:00"), "吃早饭"),
        }
};

Console.WriteLine(person.Print()); //因为使用Print重写了ToString方法，所以可以改成 Console.WriteLine(person);
```
### 结果

![image](https://github.com/remarxk/PrintUtil/assets/86111678/b2ab8e48-f9aa-4fe2-a5ea-b6aa7d49eb1d)

## 性能测试
使用BenchmarkDotNet库进行测试，与ObjectDumper，Newtonsoft.Json库进行性能比较。
### 测试代码
移除掉Person中的Price属性后，执行以下代码进行测试
```C#
BenchmarkRunner.Run<Test>();

[MemoryDiagnoser]
public class Test
{
    public Person person = new Person()
    {
        Id = 1,
        Name = "张三",
        Age = 30,
        Profession = new Profession("律师", 30000.58),
        Skills = new Skill[]
        {
            new Skill("技能1", 20.5),
            new Skill("技能2", 58.4)
        },
            Histroy = new List<Histroy>
        {
            new Histroy(DateTime.Now, "睡觉"),
            new Histroy(Convert.ToDateTime("2022/3/15 8:30:00"), "吃早饭"),
        }
    };

    [Params(100, 1000, 10000)]
    public int count;

    [Benchmark]
    public void TestObjectDumper()
    {
        for (int i = 0; i < count; i++)
        {
            person.Dump();
        }
    }

    [Benchmark]
    public void TestJson()
    {
        for (int i = 0; i < count; i++)
        {
            JsonConvert.SerializeObject(person, Formatting.Indented);
        }
    }

    [Benchmark]
    public void TestPrint()
    {
        for (int i = 0; i <= count; i++)
        {
            person.Print();
        }
    }
}
```
### 测试结果

![image](https://github.com/remarxk/PrintUtil/assets/86111678/149031fd-45eb-4f56-bc97-ee2af1afb48f)
