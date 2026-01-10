# Quick Reference: VsVim Text Objects

## What Are Text Objects?

Text objects let you select or operate on structured pieces of text. The pattern is:
```
[operator][a|i][text-object]
```

- **operator**: what to do (`d`=delete, `c`=change, `y`=yank, `v`=visual select)
- **a**: "around" (includes delimiters)
- **i**: "inner" (excludes delimiters)  
- **text-object**: what to operate on (see below)

## Quick Examples (All Working in VsVim2022!)

### Parentheses `()`
```javascript
function test(x > 0) {
    // cursor anywhere between the ()
    vi(  →  selects: x > 0
    va(  →  selects: (x > 0)
    di(  →  deletes: x > 0  (leaves empty ())
    ci(  →  deletes and enters insert mode
}
```

### Curly Braces `{}`
```javascript
if (condition) {
    // cursor anywhere inside braces
    vi{  →  selects content inside {}
    va{  →  selects including {}
    di{  →  empties the block
}
```

### Quotes `"` `'` `` ` ``
```javascript
var str = "Hello, world!";
    // cursor inside quotes
    vi"  →  selects: Hello, world!
    va"  →  selects: "Hello, world!"
    di"  →  makes it ""
    ci"  →  change the string content
```

### Square Brackets `[]`
```javascript
array[index];
    // cursor on 'index'
    vi[  →  selects: index
    di[  →  makes it []
```

### Angle Brackets `<>`
```csharp
List<int> numbers;
    // cursor on 'int'
    vi<  →  selects: int
    di<  →  makes it <>
```

### Words
```
hello world
  // cursor on 'world'
  viw  →  selects: world
  vaw  →  selects: world (+ space)
  ciw  →  change the word
```

### Paragraphs
```
paragraph 1

paragraph 2
  // cursor in paragraph 1
  vip  →  selects entire paragraph
  dap  →  deletes paragraph + blank line
```

## Combining with Operators

| Operator | Command | Effect |
|----------|---------|--------|
| **Delete** | `di(`, `da"`, `di{` | Delete text object |
| **Change** | `ci(`, `ca'`, `ci[` | Delete + enter insert mode |
| **Yank** | `yi(`, `ya{`, `yi"` | Copy text object |
| **Visual** | `vi(`, `va{`, `vi"` | Select text object |

## Working with Counts

```javascript
func(outer(inner(x)))
     // cursor on 'x'
     vi(   →  selects: x
     2vi(  →  selects: inner(x)
     3vi(  →  selects: outer(inner(x))
```

## Pro Tips

1. **Start from anywhere**: Cursor can be anywhere within the text object
2. **Works on delimiters**: If cursor is on `(` or `)`, it still works
3. **Line-wise for large blocks**: Empty lines in blocks use line-wise operations
4. **Combine with repeat**: After `di(`, press `.` to repeat on next occurrence
5. **Visual mode friendly**: Use `v` then `i(` to visually select

## All Supported Text Objects in VsVim2022

### Paired Delimiters
- `(` `)` `b` - parentheses / blocks
- `{` `}` `B` - curly braces / Blocks  
- `[` `]` - square brackets
- `<` `>` - angle brackets
- `t` - HTML/XML tags

### Quotes
- `"` - double quotes
- `'` - single quotes  
- `` ` `` - backticks

### Text Units
- `w` - word (lowercase)
- `W` - WORD (uppercase, includes punctuation)
- `s` - sentence
- `p` - paragraph

## Testing It Out

1. Open Visual Studio 2022
2. Create a test file with:
   ```javascript
   function test(x, y) {
       console.log("Hello");
   }
   ```
3. Put cursor inside `(x, y)` and press `vi(`
4. You should see `x, y` selected!

## No Configuration Needed

These features work out-of-the-box in VsVim2022. No additional setup or configuration required!
