# AssertEx.DoesNotThrow method (1 of 2)

Asserts that *assertionExpression* does not throw an exception.

```csharp
public static void DoesNotThrow(Expression<Action> assertionExpression)
```

## See Also

* class [AssertEx](../AssertEx.md)
* namespace [Faithlife.Testing](../../Faithlife.Testing.md)

---

# AssertEx.DoesNotThrow&lt;T&gt; method (2 of 2)

Asserts that *assertionExpression* does not throw an exception and allows chaining further asserts on the current value.

```csharp
public static Task<Assertable<T>> DoesNotThrow<T>(this Task<Assertable<T>> source, 
    Expression<Action<T>> assertionExpression)
    where T : class
```

## See Also

* class [Assertable&lt;T&gt;](../Assertable-1.md)
* class [AssertEx](../AssertEx.md)
* namespace [Faithlife.Testing](../../Faithlife.Testing.md)

<!-- DO NOT EDIT: generated by xmldocmd for Faithlife.Testing.dll -->