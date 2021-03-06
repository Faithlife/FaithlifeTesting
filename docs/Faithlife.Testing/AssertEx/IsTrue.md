# AssertEx.IsTrue method (1 of 2)

Asserts that *predicateExpression* returns `true`.

```csharp
public static void IsTrue(Expression<Func<bool>> predicateExpression)
```

## See Also

* class [AssertEx](../AssertEx.md)
* namespace [Faithlife.Testing](../../Faithlife.Testing.md)

---

# AssertEx.IsTrue&lt;T&gt; method (2 of 2)

Asserts that *predicateExpression* does not return `false` and allows chaining further asserts on the current value.

```csharp
public static Task<Assertable<T>> IsTrue<T>(this Task<Assertable<T>> source, 
    Expression<Func<T, bool>> predicateExpression)
    where T : class
```

## See Also

* class [Assertable&lt;T&gt;](../Assertable-1.md)
* class [AssertEx](../AssertEx.md)
* namespace [Faithlife.Testing](../../Faithlife.Testing.md)

<!-- DO NOT EDIT: generated by xmldocmd for Faithlife.Testing.dll -->
