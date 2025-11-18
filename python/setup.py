"""
Setup script for PromptResponse Python library.
"""
from setuptools import setup, find_packages
from pathlib import Path

# Read long description from README
readme_file = Path(__file__).parent / 'README.md'
long_description = readme_file.read_text(encoding='utf-8') if readme_file.exists() else ''

setup(
    name='promptresponse',
    version='0.1.0',
    description='Python library for working with APR (Adaptive Prompt Response) forms',
    long_description=long_description,
    long_description_content_type='text/markdown',
    author='PromptResponse Contributors',
    author_email='',
    url='https://github.com/marctjones/promptresponse',
    packages=find_packages(),
    python_requires='>=3.8',
    install_requires=[
        # Core dependencies (none required for basic functionality)
    ],
    extras_require={
        'signatures': [
            'cryptography>=41.0.0',
        ],
        'dev': [
            'pytest>=7.4.0',
            'pytest-cov>=4.1.0',
            'black>=23.0.0',
            'mypy>=1.5.0',
        ],
    },
    classifiers=[
        'Development Status :: 3 - Alpha',
        'Intended Audience :: Developers',
        'License :: OSI Approved :: MIT License',
        'Programming Language :: Python :: 3',
        'Programming Language :: Python :: 3.8',
        'Programming Language :: Python :: 3.9',
        'Programming Language :: Python :: 3.10',
        'Programming Language :: Python :: 3.11',
        'Programming Language :: Python :: 3.12',
        'Topic :: Software Development :: Libraries :: Python Modules',
    ],
    keywords='forms json validation signatures documents',
    project_urls={
        'Source': 'https://github.com/marctjones/promptresponse',
        'Bug Reports': 'https://github.com/marctjones/promptresponse/issues',
    },
)
