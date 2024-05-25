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
var conn = RedisExchangeManager.GetConnection("Throttle");
this.Counter = new RedisCounter(conn);
this.TS = new Throttle("ThrottleName", TimeSpan.FromSeconds(1), 10, counter:this.Counter);
~~~

### NUGET
[AsNum.Throttle](https://www.nuget.org/packages/AsNum.Throttle/)

[AsNum.Throttle.Redis](https://www.nuget.org/packages/AsNum.Throttle.Redis/)
