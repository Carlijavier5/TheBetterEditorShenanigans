using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchAndSortAlgs {

    /// <summary> An array containing all available objects;
    /// <br></br> Note: The algorithms SHOULD NOT modify this array in any way; </summary>
    [SerializeField] private readonly BaseObject[] objArray;

    /// <summary>
    /// Write your sorting method here!
    /// </summary>
    /// <param name="ingredientType"> Only object whose ingredientType is equal to this IngredientType must be included in the results list; </param>
    /// <param name="effectType"> Only objects whose custom effects list contains an object of this type should be included in the results list; </param>
    /// <returns> A list of objects that meet the right criteria; </returns>
    public List<BaseObject> ObjectSort(BaseObject.IngredientType ingredientType, System.Type effectType) {
        return null;
    }
}

/// Below are the dummy classes that make up the Base Object! ///

/// <summary>
/// This is the base object;
/// </summary>
public class BaseObject {

    public enum IngredientType {
        None,
        Chocolate,
        Cream,
        Dough,
    }

    public string name;
    public IngredientType ingredientType;
    public List<TestEffect> customEffects;

    public BaseObject(string name) {
        this.name = name;
    }
}

/// <summary> Base Test Effect; </summary>
public abstract class TestEffect {

    /// We don't care about the code that goes here;
}

/// <summary> Ice go brrr; </summary>
public class IceEffect : TestEffect {

    /// We don't care about the code that goes here either;
}

/// <summary> Fire go brrr; </summary>
public class FireEffect : TestEffect {

    /// We don't care about the code here still;
}
