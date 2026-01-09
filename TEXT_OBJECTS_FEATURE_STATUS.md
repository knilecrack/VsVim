# Text Object Motions - Feature Status

## Summary

**Good news!** All the text object motions mentioned in your issue are **already fully implemented** in VsVim, including VsVim2022!

## Available Text Object Motions

VsVim supports all the standard Vim text objects with both their `i` (inner) and `a` (around/all) forms:

### Block/Paired Delimiters

| Text Object | Description | Example Usage |
|-------------|-------------|---------------|
| `vi(` / `va(` | Inner/Around parentheses | `di(` - delete inside (), `va(` - select including () |
| `vi)` / `va)` | Same as above | Alternative syntax for parentheses |
| `vi{` / `va{` | Inner/Around curly braces | `ci{` - change inside {}, `va{` - select including {} |
| `vi}` / `va}` | Same as above | Alternative syntax for curly braces |
| `vi[` / `va[` | Inner/Around square brackets | `di[` - delete inside [], `va[` - select including [] |
| `vi]` / `va]` | Same as above | Alternative syntax for square brackets |
| `vi<` / `va<` | Inner/Around angle brackets | `di<` - delete inside <>, `va<` - select including <> |
| `vi>` / `va>` | Same as above | Alternative syntax for angle brackets |

### String Delimiters

| Text Object | Description | Example Usage |
|-------------|-------------|---------------|
| `vi"` / `va"` | Inner/Around double quotes | `ci"` - change text inside "" |
| `vi'` / `va'` | Inner/Around single quotes | `di'` - delete text inside '' |
| `vi\`` / `va\`` | Inner/Around backticks | `vi\`` - select inside backticks |

### Special Objects

| Text Object | Description | Example Usage |
|-------------|-------------|---------------|
| `vib` / `vab` | Inner/Around block (parentheses) | Alias for `vi(` / `va(` |
| `viB` / `vaB` | Inner/Around Block (curly braces) | Alias for `vi{` / `va{` |
| `vit` / `vat` | Inner/Around HTML/XML tags | `dit` - delete content between tags |

### Word, Sentence, and Paragraph Objects

| Text Object | Description | Example Usage |
|-------------|-------------|---------------|
| `viw` / `vaw` | Inner/Around normal word | `ciw` - change word under cursor |
| `viW` / `vaW` | Inner/Around WORD (bigword) | `daw` - delete WORD and trailing space |
| `vip` / `vap` | Inner/Around paragraph | `yap` - yank paragraph |
| `vis` / `vas` | Inner/Around sentence | `das` - delete sentence |

## How to Use

These text objects work with any operator:

- **Delete**: `di(`, `da"`, `di{`, etc.
- **Change**: `ci(`, `ca'`, `ci[`, etc.
- **Yank (copy)**: `yi(`, `ya{`, `yi"`, etc.
- **Visual select**: `vi(`, `va{`, `vi"`, etc.

### Examples

Given the code:
```javascript
if (x > 0) {
    console.log("Hello, world!");
}
```

- With cursor inside `(x > 0)`:
  - `vi(` → selects `x > 0`
  - `va(` → selects `(x > 0)`
  - `di(` → deletes `x > 0`, leaving `()`
  - `ci(` → deletes `x > 0` and enters insert mode

- With cursor inside the curly braces:
  - `vi{` → selects the content inside the braces
  - `va{` → selects including the braces
  - `di{` → deletes content, leaving empty `{}`

- With cursor inside the string:
  - `vi"` → selects `Hello, world!`
  - `va"` → selects `"Hello, world!"`
  - `ci"` → changes the string content

## Implementation Details

The text object motions are implemented in:

- **Definition**: `Src/VimCore/MotionCapture.fs` (lines 20-75)
- **Core Logic**: `Src/VimCore/MotionUtil.fs` (methods: `AllBlock`, `InnerBlock`, `QuotedString`, etc.)
- **Type System**: `Src/VimCore/CoreInterfaces.fs` (Motion discriminated union)

The implementation includes proper handling of:
- Nested delimiters (e.g., finding the correct matching bracket)
- Counts (e.g., `2di(` to delete inside the 2nd level of parentheses)
- Line-wise vs character-wise operations
- Edge cases (empty blocks, cursor on delimiters, etc.)

## Testing

The features are extensively tested in:
- `Test/VimCoreTest/MotionUtilTest.cs` - Unit tests for motion logic
- `Test/VimCoreTest/NormalModeIntegrationTest.cs` - Integration tests with operators
- `Test/VimCoreTest/VisualModeIntegrationTest.cs` - Visual mode tests

## Documentation

The official VsVim documentation confirms these features in:
- `Documentation/Supported Features.md` - Lists all supported text objects

## For VsVim2022 Specifically

VsVim2022 gets all these features automatically because:

1. VsVim2022 project (`Src/VsVim2022/VsVim2022.csproj`) includes a project reference to VimCore
2. VimCore contains all the text object implementation
3. There are no version-specific restrictions on these features

## Conclusion

**No additional implementation is needed!** All the text object motions mentioned in your issue (`vi(`, `va{`, `vi"`, etc.) are already working in VsVim2022. You can start using them immediately.

If you're experiencing issues with these commands, it might be:
1. A configuration issue with VsVim
2. Key binding conflicts with Visual Studio
3. VsVim not being properly enabled

To verify VsVim is working:
1. Open Visual Studio 2022
2. Ensure VsVim extension is installed and enabled
3. Open any file in a text editor
4. Try entering visual mode with `v` and then using text objects like `i(`, `a"`, etc.
