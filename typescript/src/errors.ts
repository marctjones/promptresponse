export class AprParseError extends Error {
  override name = "AprParseError";
  /**
   * The format's own diagnostic, where it names one — `WRONG_TYPE` for a structural
   * member carrying the wrong JSON type. A message says what went wrong to a person;
   * a code says it to the conformance suite.
   */
  code?: string;
  constructor(message: string, code?: string) {
    super(message);
    this.code = code;
  }
}
