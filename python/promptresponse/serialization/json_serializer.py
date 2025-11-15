"""
JSON serialization for APR documents.
"""
import json
from typing import Union, TextIO
from pathlib import Path
from ..models import AprDocument


class AprJsonSerializer:
    """Serializer for APR documents to/from JSON."""

    @staticmethod
    def serialize(document: AprDocument, indent: int = 2) -> str:
        """
        Serialize an APR document to JSON string.

        Args:
            document: The APR document to serialize
            indent: Number of spaces for indentation (default: 2)

        Returns:
            JSON string representation
        """
        return json.dumps(document.to_dict(), indent=indent, ensure_ascii=False)

    @staticmethod
    def deserialize(json_str: str) -> AprDocument:
        """
        Deserialize an APR document from JSON string.

        Args:
            json_str: JSON string to deserialize

        Returns:
            APR document object

        Raises:
            ValueError: If JSON is invalid or doesn't match APR schema
        """
        try:
            data = json.loads(json_str)
            return AprDocument.from_dict(data)
        except (json.JSONDecodeError, KeyError, TypeError) as e:
            raise ValueError(f"Invalid APR JSON: {e}") from e

    @staticmethod
    def load_file(file_path: Union[str, Path]) -> AprDocument:
        """
        Load an APR document from a JSON file.

        Args:
            file_path: Path to the APR file

        Returns:
            APR document object

        Raises:
            FileNotFoundError: If file doesn't exist
            ValueError: If file content is invalid
        """
        path = Path(file_path)
        if not path.exists():
            raise FileNotFoundError(f"APR file not found: {file_path}")

        with open(path, 'r', encoding='utf-8') as f:
            return AprJsonSerializer.deserialize(f.read())

    @staticmethod
    def save_file(document: AprDocument, file_path: Union[str, Path], indent: int = 2) -> None:
        """
        Save an APR document to a JSON file.

        Args:
            document: The APR document to save
            file_path: Path where to save the file
            indent: Number of spaces for indentation (default: 2)
        """
        path = Path(file_path)
        path.parent.mkdir(parents=True, exist_ok=True)

        with open(path, 'w', encoding='utf-8') as f:
            f.write(AprJsonSerializer.serialize(document, indent=indent))

    @staticmethod
    def load_stream(stream: TextIO) -> AprDocument:
        """
        Load an APR document from a text stream.

        Args:
            stream: Text stream to read from

        Returns:
            APR document object
        """
        return AprJsonSerializer.deserialize(stream.read())

    @staticmethod
    def save_stream(document: AprDocument, stream: TextIO, indent: int = 2) -> None:
        """
        Save an APR document to a text stream.

        Args:
            document: The APR document to save
            stream: Text stream to write to
            indent: Number of spaces for indentation (default: 2)
        """
        stream.write(AprJsonSerializer.serialize(document, indent=indent))
