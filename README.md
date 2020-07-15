# Throttle
Action throttle in a period.

If you want execute some action 10 times in one second, you can use it like this:

### InProcess
~~~
Throttle TS = new Throttle("ThrottleName", TimeSpan.FromSeconds(1), 10);
...
this.TS.Execute(()=> ...);
...
~~~


### Cross Process
~~~
this.Block = new CrossProcessBlock();
var conn = RedisExchangeManager.GetConnection(RedisNames.Mix);
this.Counter = new RedisCounter(conn);
this.TS = new Throttle("ThrottleName", TimeSpan.FromSeconds(1), 10, this.Block, this.Counter);
~~~


### Cross Server 
**Nolonger available, use DefaultBlock instead.**
~~~
this.Block = new RedisBlock(conn);
~~~

### NUGET
[AsNum.Throttle](https://www.nuget.org/packages/AsNum.Throttle/)

[AsNum.Throttle.CrossProcess](https://www.nuget.org/packages/AsNum.Throttle.CrossProcess/)

[AsNum.Throttle.Redis](https://www.nuget.org/packages/AsNum.Throttle.Redis/)

[AsNum.Throttle.Statistic](https://www.nuget.org/packages/AsNum.Throttle.Statistic/) **Nolonger available**
